namespace Tablix.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence;
    using Tablix.Core.Settings;

    /// <summary>
    /// Background health monitor for configured model providers.
    /// </summary>
    public class ModelProviderHealthCheckService : IDisposable
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Persistence;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[ModelProviderHealthCheckService] ";
        private readonly HttpClient _HttpClient = new HttpClient();
        private readonly ConcurrentDictionary<string, ModelProviderSettings> _Providers = new ConcurrentDictionary<string, ModelProviderSettings>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, EndpointHealthState> _States = new ConcurrentDictionary<string, EndpointHealthState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> _NextChecksUtc = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private readonly object _ScheduleLock = new object();
        private readonly object _LifecycleLock = new object();
        private CancellationTokenSource _LoopCancellation = null;
        private Task _LoopTask = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="logging">Logging module.</param>
        public ModelProviderHealthCheckService(DatabaseDriverBase persistence, LoggingModule logging)
        {
            _Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _Logging = logging ?? new LoggingModule();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start background monitoring.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public void Start(CancellationToken token = default)
        {
            lock (_LifecycleLock)
            {
                if (_LoopTask != null) return;

                _LoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                _LoopTask = Task.Run(() => RunAsync(_LoopCancellation.Token));
            }
        }

        /// <summary>
        /// Stop background monitoring.
        /// </summary>
        public async Task StopAsync()
        {
            Task loopTask = null;
            lock (_LifecycleLock)
            {
                if (_LoopCancellation == null) return;
                _LoopCancellation.Cancel();
                loopTask = _LoopTask;
            }

            if (loopTask != null)
            {
                try
                {
                    await loopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        /// <summary>
        /// Reload persisted model providers into the monitor.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task RefreshProvidersAsync(CancellationToken token = default)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int skip = 0;

            while (true)
            {
                List<ModelProviderSettings> batch = await _Persistence.ModelProviders.EnumerateAsync(1000, skip, null, null, token).ConfigureAwait(false);
                if (batch == null || batch.Count == 0) break;

                foreach (ModelProviderSettings provider in batch)
                {
                    OnProviderSaved(provider);
                    seen.Add(provider.Id);
                }

                skip += batch.Count;
                if (batch.Count < 1000) break;
            }

            foreach (string providerId in _Providers.Keys.ToList())
            {
                if (!seen.Contains(providerId))
                    OnProviderDeleted(providerId);
            }
        }

        /// <summary>
        /// Add or update one provider in the monitor.
        /// </summary>
        /// <param name="provider">Provider settings.</param>
        public void OnProviderSaved(ModelProviderSettings provider)
        {
            if (provider == null || String.IsNullOrWhiteSpace(provider.Id)) return;

            ModelProviderSettings copy = CopyProvider(provider);
            ModelProviderSettings.ApplyHealthCheckDefaults(copy);
            _Providers[copy.Id] = copy;

            EndpointHealthState state = _States.GetOrAdd(copy.Id, _ => CreateState(copy));
            lock (state.SyncRoot)
            {
                state.EndpointId = copy.Id;
                state.EndpointName = copy.Name ?? copy.Id;
                state.HealthCheckEnabled = copy.Enabled && copy.HealthCheckEnabled;
                if (!state.HealthCheckEnabled)
                {
                    state.IsHealthy = true;
                    state.ConsecutiveSuccesses = 0;
                    state.ConsecutiveFailures = 0;
                    state.LastError = null;
                }
            }

            lock (_ScheduleLock)
            {
                if (copy.Enabled && copy.HealthCheckEnabled)
                    _NextChecksUtc[copy.Id] = DateTime.MinValue;
                else
                    _NextChecksUtc.Remove(copy.Id);
            }
        }

        /// <summary>
        /// Remove a provider from monitoring.
        /// </summary>
        /// <param name="providerId">Provider identifier.</param>
        public void OnProviderDeleted(string providerId)
        {
            if (String.IsNullOrWhiteSpace(providerId)) return;

            _Providers.TryRemove(providerId, out _);
            _States.TryRemove(providerId, out _);
            lock (_ScheduleLock)
            {
                _NextChecksUtc.Remove(providerId);
            }
        }

        /// <summary>
        /// Get health status for one provider.
        /// </summary>
        /// <param name="providerId">Provider identifier.</param>
        /// <returns>Health status.</returns>
        public EndpointHealthStatus GetHealthStatus(string providerId)
        {
            if (String.IsNullOrWhiteSpace(providerId)) return null;
            return _States.TryGetValue(providerId, out EndpointHealthState state) ? EndpointHealthStatus.FromState(state) : null;
        }

        /// <summary>
        /// Get health status for all monitored providers.
        /// </summary>
        /// <returns>Health statuses.</returns>
        public List<EndpointHealthStatus> GetAllHealthStatuses()
        {
            return _States.Values
                .Select(EndpointHealthStatus.FromState)
                .Where(status => status != null)
                .OrderBy(status => status.EndpointName)
                .ThenBy(status => status.EndpointId)
                .ToList();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _LoopCancellation?.Cancel();
            _LoopCancellation?.Dispose();
            _HttpClient.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task RunAsync(CancellationToken token)
        {
            _Logging.Info(_Header + "started");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    DateTime now = DateTime.UtcNow;
                    List<ModelProviderSettings> dueProviders = GetDueProviders(now);
                    Dictionary<string, HealthCheckResult> completedChecks = new Dictionary<string, HealthCheckResult>(StringComparer.OrdinalIgnoreCase);

                    foreach (ModelProviderSettings provider in dueProviders)
                    {
                        token.ThrowIfCancellationRequested();
                        string monitorKey = BuildMonitorKey(provider);
                        if (!completedChecks.TryGetValue(monitorKey, out HealthCheckResult result))
                        {
                            result = await PerformCheckAsync(provider, token).ConfigureAwait(false);
                            completedChecks[monitorKey] = result;
                        }

                        UpdateState(provider, result, DateTime.UtcNow);
                        ScheduleNextCheck(provider, DateTime.UtcNow);
                    }

                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _Logging.Warn(_Header + "health monitor loop failed: " + ex.Message);
                    try
                    {
                        await Task.Delay(1000, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            _Logging.Info(_Header + "stopped");
        }

        private List<ModelProviderSettings> GetDueProviders(DateTime now)
        {
            List<ModelProviderSettings> providers = new List<ModelProviderSettings>();
            foreach (ModelProviderSettings provider in _Providers.Values)
            {
                if (provider == null || !provider.Enabled || !provider.HealthCheckEnabled) continue;

                bool due = false;
                lock (_ScheduleLock)
                {
                    if (!_NextChecksUtc.TryGetValue(provider.Id, out DateTime next))
                    {
                        next = DateTime.MinValue;
                        _NextChecksUtc[provider.Id] = next;
                    }

                    due = next <= now;
                }

                if (due)
                    providers.Add(CopyProvider(provider));
            }

            return providers;
        }

        private async Task<HealthCheckResult> PerformCheckAsync(ModelProviderSettings provider, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (String.IsNullOrWhiteSpace(provider.HealthCheckUrl))
                    throw new ArgumentException("Health check URL is required.");

                if (!Uri.TryCreate(provider.HealthCheckUrl, UriKind.Absolute, out Uri uri))
                    throw new ArgumentException("Health check URL is not absolute.");

                HttpMethod method = provider.HealthCheckMethod == HealthCheckMethodEnum.HEAD ? HttpMethod.Head : HttpMethod.Get;
                using HttpRequestMessage request = new HttpRequestMessage(method, uri);
                if (provider.HealthCheckUseAuth && !String.IsNullOrWhiteSpace(provider.ApiKey))
                {
                    if (provider.Type == ModelProviderTypeEnum.Gemini)
                        request.Headers.TryAddWithoutValidation("x-goog-api-key", provider.ApiKey);
                    else
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
                }

                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(provider.HealthCheckTimeoutMs);

                using HttpResponseMessage response = await _HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                stopwatch.Stop();

                int statusCode = (int)response.StatusCode;
                bool success = statusCode == provider.HealthCheckExpectedStatusCode;
                return new HealthCheckResult
                {
                    Success = success,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                    Error = success ? null : "Expected HTTP " + provider.HealthCheckExpectedStatusCode + " but received HTTP " + statusCode + "."
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new HealthCheckResult
                {
                    Success = false,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                    Error = Sanitize(ex.Message, provider.ApiKey)
                };
            }
        }

        private void UpdateState(ModelProviderSettings provider, HealthCheckResult result, DateTime timestampUtc)
        {
            EndpointHealthState state = _States.GetOrAdd(provider.Id, _ => CreateState(provider));
            lock (state.SyncRoot)
            {
                state.EndpointName = provider.Name ?? provider.Id;
                state.HealthCheckEnabled = provider.Enabled && provider.HealthCheckEnabled;

                if (state.LastCheckUtc.HasValue)
                {
                    long elapsedMs = Math.Max(0, (long)(timestampUtc - state.LastCheckUtc.Value).TotalMilliseconds);
                    if (state.IsHealthy) state.TotalUptimeMs += elapsedMs;
                    else state.TotalDowntimeMs += elapsedMs;
                }
                else
                {
                    state.FirstCheckUtc = timestampUtc;
                    state.LastStateChangeUtc = timestampUtc;
                }

                state.LastCheckUtc = timestampUtc;
                state.CheckHistory.Add(new HealthCheckRecord { TimestampUtc = timestampUtc, Success = result.Success });
                PruneHistory(state, timestampUtc.AddHours(-24));

                if (result.Success)
                {
                    state.ConsecutiveSuccesses++;
                    state.ConsecutiveFailures = 0;
                    state.LastHealthyUtc = timestampUtc;
                    state.LastError = null;

                    if (!state.IsHealthy && state.ConsecutiveSuccesses >= provider.HealthyThreshold)
                    {
                        state.IsHealthy = true;
                        state.LastStateChangeUtc = timestampUtc;
                    }
                }
                else
                {
                    state.ConsecutiveFailures++;
                    state.ConsecutiveSuccesses = 0;
                    state.LastUnhealthyUtc = timestampUtc;
                    state.LastError = result.Error;

                    if (state.IsHealthy && state.ConsecutiveFailures >= provider.UnhealthyThreshold)
                    {
                        state.IsHealthy = false;
                        state.LastStateChangeUtc = timestampUtc;
                    }
                }
            }
        }

        private static void PruneHistory(EndpointHealthState state, DateTime cutoffUtc)
        {
            while (state.CheckHistory.Count > 0 && state.CheckHistory[0].TimestampUtc < cutoffUtc)
                state.CheckHistory.RemoveAt(0);
        }

        private void ScheduleNextCheck(ModelProviderSettings provider, DateTime now)
        {
            lock (_ScheduleLock)
            {
                if (provider.Enabled && provider.HealthCheckEnabled)
                    _NextChecksUtc[provider.Id] = now.AddMilliseconds(provider.HealthCheckIntervalMs);
                else
                    _NextChecksUtc.Remove(provider.Id);
            }
        }

        private static EndpointHealthState CreateState(ModelProviderSettings provider)
        {
            return new EndpointHealthState
            {
                EndpointId = provider.Id,
                EndpointName = provider.Name ?? provider.Id,
                HealthCheckEnabled = provider.Enabled && provider.HealthCheckEnabled,
                IsHealthy = true
            };
        }

        private static string BuildMonitorKey(ModelProviderSettings provider)
        {
            return String.Join("|",
                provider.Type.ToString(),
                provider.HealthCheckMethod.ToString(),
                provider.HealthCheckUrl ?? String.Empty,
                provider.HealthCheckExpectedStatusCode.ToString(),
                provider.HealthCheckUseAuth && !String.IsNullOrWhiteSpace(provider.ApiKey) ? provider.ApiKey : String.Empty);
        }

        private static ModelProviderSettings CopyProvider(ModelProviderSettings provider)
        {
            return new ModelProviderSettings
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                Endpoint = provider.Endpoint,
                ApiKey = provider.ApiKey,
                Model = provider.Model,
                SystemPrompt = provider.SystemPrompt,
                Enabled = provider.Enabled,
                DefaultStreaming = provider.DefaultStreaming,
                SupportsNativeToolCalls = provider.SupportsNativeToolCalls,
                UseNativeToolCalls = provider.UseNativeToolCalls,
                SupportsStrictJson = provider.SupportsStrictJson,
                ToolCapabilityNote = provider.ToolCapabilityNote,
                Temperature = provider.Temperature,
                TopP = provider.TopP,
                MaxTokens = provider.MaxTokens,
                RequestTimeoutMs = provider.RequestTimeoutMs,
                MaxConcurrentRequests = provider.MaxConcurrentRequests,
                HealthCheckEnabled = provider.HealthCheckEnabled,
                HealthCheckUrl = provider.HealthCheckUrl,
                HealthCheckMethod = provider.HealthCheckMethod,
                HealthCheckIntervalMs = provider.HealthCheckIntervalMs,
                HealthCheckTimeoutMs = provider.HealthCheckTimeoutMs,
                HealthCheckExpectedStatusCode = provider.HealthCheckExpectedStatusCode,
                HealthyThreshold = provider.HealthyThreshold,
                UnhealthyThreshold = provider.UnhealthyThreshold,
                HealthCheckUseAuth = provider.HealthCheckUseAuth
            };
        }

        private static string Sanitize(string message, string apiKey)
        {
            string sanitized = message ?? String.Empty;
            if (!String.IsNullOrEmpty(apiKey))
                sanitized = sanitized.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
            return sanitized;
        }

        #endregion

        private class HealthCheckResult
        {
            public bool Success { get; set; } = false;
            public double TotalMs { get; set; } = 0;
            public string Error { get; set; } = null;
        }
    }
}
