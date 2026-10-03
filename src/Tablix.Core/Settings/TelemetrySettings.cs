namespace Tablix.Core.Settings
{
    using System;

    /// <summary>
    /// Telemetry export settings (metrics, traces, and logs through Radiant). Defaults bind and export to the
    /// loopback address 127.0.0.1; set container or collector hostnames explicitly for a shared deployment.
    /// Applied at startup; changes take effect on restart.
    /// </summary>
    public class TelemetrySettings
    {
        #region Public-Members

        /// <summary>
        /// Master switch. When false, no telemetry host starts and nothing is exported; the application still emits
        /// into its in-process meters at near-zero cost. Default true.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Service name stamped on every metric, span, and log as service.name. Default "tablix-server".
        /// Null or empty values reset to the default.
        /// </summary>
        public string ServiceName
        {
            get { return _ServiceName; }
            set { _ServiceName = String.IsNullOrWhiteSpace(value) ? "tablix-server" : value.Trim(); }
        }

        /// <summary>
        /// Whether traces, metrics, and logs are pushed over OTLP to <see cref="OtlpEndpoint"/>. Default true.
        /// Point this at an OpenTelemetry Collector (the bundled Docker stack runs one) so each signal reaches the
        /// right backend.
        /// </summary>
        public bool OtlpEnable { get; set; } = true;

        /// <summary>
        /// OTLP endpoint. Use the gRPC port (4317) with protocol "grpc", or the HTTP port (4318) with protocol
        /// "httpprotobuf". Default "http://127.0.0.1:4317". Must be an absolute URI; invalid values reset to the default.
        /// </summary>
        public string OtlpEndpoint
        {
            get { return _OtlpEndpoint; }
            set { _OtlpEndpoint = IsAbsoluteUri(value) ? value.Trim() : "http://127.0.0.1:4317"; }
        }

        /// <summary>
        /// OTLP protocol: "grpc" (default) or "httpprotobuf". Any other value resets to "grpc".
        /// </summary>
        public string OtlpProtocol
        {
            get { return _OtlpProtocol; }
            set { _OtlpProtocol = String.Equals(value, "httpprotobuf", StringComparison.OrdinalIgnoreCase) ? "httpprotobuf" : "grpc"; }
        }

        /// <summary>
        /// Whether an in-process Prometheus scrape endpoint is served on its own port. Default true.
        /// The endpoint is anonymous by design; keep it on an internal network.
        /// </summary>
        public bool PrometheusEnable { get; set; } = true;

        /// <summary>
        /// Hostname the Prometheus scrape endpoint binds. Default "127.0.0.1". The endpoint answers only requests whose
        /// Host header matches this name, and wildcards ("*", "+", "0.0.0.0") are rejected by the underlying exporter.
        /// Inside a container, set it to the service name the scraper uses (the bundled Docker stack uses
        /// "tablix-server"). Null or empty values reset to the default.
        /// </summary>
        public string PrometheusHostname
        {
            get { return _PrometheusHostname; }
            set { _PrometheusHostname = String.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim(); }
        }

        /// <summary>
        /// Port of the Prometheus scrape endpoint. Default 9464. Minimum 1, maximum 65535 (values are clamped).
        /// </summary>
        public int PrometheusPort
        {
            get { return _PrometheusPort; }
            set { _PrometheusPort = Math.Clamp(value, 1, 65535); }
        }

        /// <summary>
        /// Path of the Prometheus scrape endpoint. Default "/metrics". A leading slash is added when missing; null or
        /// empty values reset to the default.
        /// </summary>
        public string PrometheusPath
        {
            get { return _PrometheusPath; }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) _PrometheusPath = "/metrics";
                else _PrometheusPath = value.Trim().StartsWith("/", StringComparison.Ordinal) ? value.Trim() : "/" + value.Trim();
            }
        }

        /// <summary>
        /// Whether logs are also pushed directly to a Loki 3.x OTLP endpoint (for deployments without a collector).
        /// Default false. Leave false when <see cref="OtlpEndpoint"/> is a collector that forwards logs to Loki, or
        /// every log line is delivered twice.
        /// </summary>
        public bool LokiEnable { get; set; } = false;

        /// <summary>
        /// Loki OTLP base endpoint; "/v1/logs" is appended. Default "http://127.0.0.1:3100/otlp". Must be an absolute
        /// URI; invalid values reset to the default.
        /// </summary>
        public string LokiEndpoint
        {
            get { return _LokiEndpoint; }
            set { _LokiEndpoint = IsAbsoluteUri(value) ? value.Trim() : "http://127.0.0.1:3100/otlp"; }
        }

        /// <summary>
        /// Whether Tablix log messages are forwarded into the telemetry log pipeline (correlated with the active
        /// trace and span). Default true.
        /// </summary>
        public bool ExportLogs { get; set; } = true;

        /// <summary>
        /// Trace sampling ratio for new root traces (parent-based). Default 1.0 (sample everything). Minimum 0.0,
        /// maximum 1.0 (values are clamped).
        /// </summary>
        public double TraceSamplingRatio
        {
            get { return _TraceSamplingRatio; }
            set { _TraceSamplingRatio = Double.IsNaN(value) ? 1.0 : Math.Clamp(value, 0.0, 1.0); }
        }

        /// <summary>
        /// Metric push interval for OTLP in milliseconds. Default 15000. Minimum 1000, maximum 300000 (values are clamped).
        /// </summary>
        public int MetricExportIntervalMs
        {
            get { return _MetricExportIntervalMs; }
            set { _MetricExportIntervalMs = Math.Clamp(value, 1000, 300000); }
        }

        #endregion

        #region Private-Members

        private string _ServiceName = "tablix-server";
        private string _OtlpEndpoint = "http://127.0.0.1:4317";
        private string _OtlpProtocol = "grpc";
        private string _PrometheusHostname = "127.0.0.1";
        private int _PrometheusPort = 9464;
        private string _PrometheusPath = "/metrics";
        private string _LokiEndpoint = "http://127.0.0.1:3100/otlp";
        private double _TraceSamplingRatio = 1.0;
        private int _MetricExportIntervalMs = 15000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetrySettings()
        {
        }

        #endregion

        #region Private-Methods

        private static bool IsAbsoluteUri(string value)
        {
            return !String.IsNullOrWhiteSpace(value) && Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri _);
        }

        #endregion
    }
}
