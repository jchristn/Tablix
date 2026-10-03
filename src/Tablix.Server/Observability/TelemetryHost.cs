namespace Tablix.Server.Observability
{
    using System;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Radiant;
    using SyslogLogging;
    using Tablix.Core.Observability;
    using Tablix.Core.Settings;

    /// <summary>
    /// The single telemetry host for the Tablix server process (the composition root for Radiant). Subscribes to the
    /// Tablix and Watson meters and activity sources plus the .NET HTTP client meter, exports over OTLP, serves an
    /// in-process Prometheus endpoint, optionally pushes logs to Loki, and forwards Tablix log messages into the
    /// correlated log pipeline. Everything is best-effort: a start failure leaves the server running without export.
    /// Thread safety: safe for concurrent use; dispose once on shutdown.
    /// </summary>
    public class TelemetryHost : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Name of Watson's HTTP server duration histogram, which the host gives seconds-scale buckets.
        /// </summary>
        public const string HttpServerRequestDuration = "http.server.request.duration";

        /// <summary>
        /// Whether a Radiant host is running and exporting.
        /// </summary>
        public bool IsRunning
        {
            get { return _Host != null; }
        }

        /// <summary>
        /// The Prometheus scrape URL when the endpoint is enabled and the host is running; otherwise null.
        /// </summary>
        public string PrometheusUrl
        {
            get { return _PrometheusUrl; }
        }

        /// <summary>
        /// The reason the host is not running (disabled, or the start error message); null while running.
        /// </summary>
        public string StatusMessage
        {
            get { return _StatusMessage; }
        }

        #endregion

        #region Private-Members

        private readonly object _Lock = new object();
        private RadiantHost _Host = null;
        private ILogger _Logger = null;
        private LoggingModule _Logging = null;
        private string _PrometheusUrl = null;
        private string _StatusMessage = null;
        private int _Disposed = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the telemetry host from settings. Never throws: when disabled or when start fails, the returned host
        /// is inert and <see cref="StatusMessage"/> explains why.
        /// </summary>
        /// <param name="settings">Telemetry settings. Null disables telemetry export.</param>
        /// <returns>The telemetry host.</returns>
        public static TelemetryHost Start(TelemetrySettings settings)
        {
            TelemetryHost host = new TelemetryHost();
            if (settings == null || !settings.Enable)
            {
                host._StatusMessage = "telemetry export disabled in settings";
                return host;
            }

            try
            {
                RadiantSettings radiant = BuildRadiantSettings(settings);
                host._Host = RadiantHost.Start(radiant);
                if (settings.PrometheusEnable) host._PrometheusUrl = radiant.Prometheus.ToScrapeUrl();
                if (settings.ExportLogs) host._Logger = host._Host.CreateLogger("Tablix.Server");
            }
            catch (Exception ex)
            {
                host._Host = null;
                host._StatusMessage = "telemetry export disabled (start failed): " + Describe(ex);
            }

            return host;
        }

        /// <summary>
        /// Build the Radiant settings for a Tablix server. Exposed for tests and diagnostics.
        /// </summary>
        /// <param name="settings">Telemetry settings.</param>
        /// <returns>Radiant settings subscribed to every Tablix, Watson, and HTTP client source.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="settings"/> is null.</exception>
        public static RadiantSettings BuildRadiantSettings(TelemetrySettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            RadiantSettings radiant = new RadiantSettings(settings.ServiceName) { Enable = true };
            radiant.Sources.AddMeter(TelemetryNames.MeterName);
            radiant.Sources.AddActivitySource(TelemetryNames.ActivitySourceName);
            radiant.Sources.AddMeter(TelemetryNames.WatsonSourceName);
            radiant.Sources.AddActivitySource(TelemetryNames.WatsonSourceName);
            radiant.Sources.AddMeter(TelemetryNames.HttpClientMeterName);

            radiant.Metrics.IncludeRuntime = true;
            radiant.Metrics.ExportIntervalMs = settings.MetricExportIntervalMs;
            radiant.Metrics.DefineAll(TelemetryCatalog.All);

            // Watson records http.server.request.duration in seconds without bucket advice, so the SDK would fall back
            // to millisecond-scale default boundaries (5, 10, 25, ... 10000) and every request would land in the first
            // bucket. A seconds-scale view keeps HTTP latency quantiles meaningful.
            radiant.Metrics.Define(Convention.Histogram(
                HttpServerRequestDuration,
                "s",
                TablixMetrics.DefaultBuckets,
                "http.request.method",
                "http.response.status_code",
                "http.route",
                "url.scheme",
                "network.protocol.version",
                "error.type"));
            radiant.Traces.SamplingRatio = settings.TraceSamplingRatio;
            radiant.Logs.Enable = settings.ExportLogs;

            radiant.Otlp.Enable = settings.OtlpEnable;
            radiant.Otlp.Endpoint = settings.OtlpEndpoint;
            radiant.Otlp.Protocol = String.Equals(settings.OtlpProtocol, "httpprotobuf", StringComparison.OrdinalIgnoreCase)
                ? OtlpProtocolEnum.HttpProtobuf
                : OtlpProtocolEnum.Grpc;

            radiant.Prometheus.Enable = settings.PrometheusEnable;
            radiant.Prometheus.Hostname = settings.PrometheusHostname;
            radiant.Prometheus.Port = settings.PrometheusPort;
            radiant.Prometheus.Path = settings.PrometheusPath;

            radiant.Loki.Enable = settings.LokiEnable;
            radiant.Loki.Endpoint = settings.LokiEndpoint;
            return radiant;
        }

        private TelemetryHost()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Forward every message written to <paramref name="logging"/> into the telemetry log pipeline, stamped with
        /// the active trace and span. No-op when the host is not running or log export is off. Call once.
        /// </summary>
        /// <param name="logging">Logging module. Null is ignored.</param>
        public void AttachLogging(LoggingModule logging)
        {
            if (logging == null || _Logger == null) return;

            lock (_Lock)
            {
                if (_Logging != null) return;
                _Logging = logging;
                _Logging.MessageLogged += OnMessageLogged;
            }
        }

        /// <summary>
        /// Flush pending telemetry, stop exporters, release the Prometheus port, and detach from logging.
        /// Never throws.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _Disposed, 1) == 1) return;

            lock (_Lock)
            {
                if (_Logging != null)
                {
                    try { _Logging.MessageLogged -= OnMessageLogged; } catch (Exception) { }
                    _Logging = null;
                }

                _Logger = null;
            }

            try
            {
                _Host?.Dispose();
            }
            catch (Exception)
            {
            }

            _Host = null;
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private void OnMessageLogged(LogEntry entry)
        {
            ILogger logger = _Logger;
            if (logger == null || entry == null) return;

            try
            {
                LogLevel level = ToLogLevel(entry.Severity);
                if (!logger.IsEnabled(level)) return;
                logger.Log(level, default(EventId), entry.Message, entry.Exception, (state, ex) => state);
            }
            catch (Exception)
            {
                // Log export is best-effort and must never interrupt logging.
            }
        }

        private static string Describe(Exception ex)
        {
            string message = ex.Message;
            for (Exception inner = ex.InnerException; inner != null; inner = inner.InnerException)
                message += " -> " + inner.GetType().Name + ": " + inner.Message;
            return message;
        }

        private static LogLevel ToLogLevel(Severity severity)
        {
            switch (severity)
            {
                case Severity.Debug: return LogLevel.Debug;
                case Severity.Info: return LogLevel.Information;
                case Severity.Warn: return LogLevel.Warning;
                case Severity.Error: return LogLevel.Error;
                default: return LogLevel.Critical;
            }
        }

        #endregion
    }
}
