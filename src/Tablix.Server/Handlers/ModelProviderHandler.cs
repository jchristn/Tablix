namespace Tablix.Server.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using WatsonWebserver.Core;
    using SyslogLogging;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence;
    using Tablix.Core.Settings;
    using Tablix.Server.Services;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;

    /// <summary>
    /// REST handlers for persisted model provider management.
    /// </summary>
    public class ModelProviderHandler
    {
        private readonly SettingsManager _SettingsManager;
        private readonly DatabaseDriverBase _Persistence;
        private readonly LoggingModule _Logging;
        private readonly ModelProviderHealthCheckService _HealthChecks;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="logging">Logging module.</param>
        /// <param name="healthChecks">Model provider health check service.</param>
        public ModelProviderHandler(SettingsManager settingsManager, DatabaseDriverBase persistence, LoggingModule logging, ModelProviderHealthCheckService healthChecks = null)
        {
            _SettingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _Logging = logging ?? new LoggingModule();
            _HealthChecks = healthChecks;
        }

        /// <summary>
        /// List model providers.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Paginated provider summaries.</returns>
        public async Task<object> ListProvidersAsync(ApiRequest req)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ReadEnumerationQuery(req, out int maxResults, out int skip, out string filter, out bool? enabled);

            long totalRecords = await _Persistence.ModelProviders.CountAsync(filter, enabled, req.CancellationToken).ConfigureAwait(false);
            List<ModelProviderSettings> providers = await _Persistence.ModelProviders.EnumerateAsync(maxResults, skip, filter, enabled, req.CancellationToken).ConfigureAwait(false);
            List<ModelProviderSummary> summaries = providers.Select(ToSummary).Where(summary => summary != null).ToList();
            long remaining = Math.Max(0, totalRecords - skip - summaries.Count);
            stopwatch.Stop();

            return new EnumerationResult<ModelProviderSummary>
            {
                Success = true,
                MaxResults = maxResults,
                Skip = skip,
                TotalRecords = totalRecords,
                RecordsRemaining = remaining,
                EndOfResults = remaining == 0,
                NextSkip = remaining == 0 ? null : (int?)(skip + summaries.Count),
                TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                Objects = summaries
            };
        }

        /// <summary>
        /// Read one model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Redacted provider details.</returns>
        public async Task<object> GetProviderAsync(ApiRequest req)
        {
            string id = req.Parameters["id"];
            ModelProviderSettings provider = await _Persistence.ModelProviders.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (provider == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + id + "' not found.");
            }

            return ToRead(provider);
        }

        /// <summary>
        /// List model provider health statuses.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Health statuses.</returns>
        public async Task<object> ListProviderHealthAsync(ApiRequest req)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (_HealthChecks != null)
                return _HealthChecks.GetAllHealthStatuses();

            List<ModelProviderSettings> providers = await _Persistence.ModelProviders.EnumerateAsync(1000, 0, null, null, req.CancellationToken).ConfigureAwait(false);
            return providers.Select(CreateInitialHealthStatus).Where(status => status != null).ToList();
        }

        /// <summary>
        /// Read one model provider health status.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Health status.</returns>
        public async Task<object> GetProviderHealthAsync(ApiRequest req)
        {
            string id = req.Parameters["id"];
            ModelProviderSettings provider = await _Persistence.ModelProviders.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (provider == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + id + "' not found.");
            }

            EndpointHealthStatus status = _HealthChecks?.GetHealthStatus(id);
            if (status == null && _HealthChecks != null)
            {
                _HealthChecks.OnProviderSaved(provider);
                status = _HealthChecks.GetHealthStatus(id);
            }

            return status ?? CreateInitialHealthStatus(provider);
        }

        /// <summary>
        /// Create a model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Created provider summary.</returns>
        public async Task<object> AddProviderAsync(ApiRequest req)
        {
            ModelProviderUpdate request = req.GetData<ModelProviderUpdate>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            try
            {
                ModelProviderSettings provider = ToSettings(request);
                ModelProviderSettings created = await _Persistence.ModelProviders.CreateAsync(provider, req.CancellationToken).ConfigureAwait(false);
                _HealthChecks?.OnProviderSaved(created);
                await RepairDefaultProviderIdAsync(req.CancellationToken).ConfigureAwait(false);
                req.Http.Response.StatusCode = 201;
                return ToSummary(created);
            }
            catch (InvalidOperationException ex)
            {
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, Sanitize(ex.Message, request.ApiKey));
            }
            catch (ArgumentException ex)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, Sanitize(ex.Message, request.ApiKey));
            }
        }

        /// <summary>
        /// Update a model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Updated provider summary.</returns>
        public async Task<object> UpdateProviderAsync(ApiRequest req)
        {
            string id = req.Parameters["id"];
            ModelProviderUpdate request = req.GetData<ModelProviderUpdate>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            request.Id = id;
            ModelProviderSettings existing = await _Persistence.ModelProviders.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (existing == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + id + "' not found.");
            }

            ModelProviderSettings provider = ToSettings(request);
            if (request.ClearApiKey)
                provider.ApiKey = null;
            else if (String.IsNullOrEmpty(request.ApiKey))
                provider.ApiKey = existing.ApiKey;

            ModelProviderSettings updated = await _Persistence.ModelProviders.UpdateAsync(provider, true, req.CancellationToken).ConfigureAwait(false);
            _HealthChecks?.OnProviderSaved(updated);
            await RepairDefaultProviderIdAsync(req.CancellationToken).ConfigureAwait(false);
            return ToSummary(updated);
        }

        /// <summary>
        /// Delete a model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Null response.</returns>
        public async Task<object> DeleteProviderAsync(ApiRequest req)
        {
            string id = req.Parameters["id"];
            bool deleted = await _Persistence.ModelProviders.DeleteAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (!deleted)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + id + "' not found.");
            }

            await RepairDefaultProviderIdAsync(req.CancellationToken).ConfigureAwait(false);
            _HealthChecks?.OnProviderDeleted(id);
            req.Http.Response.StatusCode = 204;
            return null;
        }

        /// <summary>
        /// Test a saved model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Connectivity test result.</returns>
        public async Task<object> TestSavedProviderAsync(ApiRequest req)
        {
            string id = req.Parameters["id"];
            ModelProviderSettings provider = await _Persistence.ModelProviders.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (provider == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + id + "' not found.");
            }

            return await TestProviderAsync(provider, req.CancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Test unsaved model provider settings.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Connectivity test result.</returns>
        public async Task<object> TestProviderRequestAsync(ApiRequest req)
        {
            ProviderConnectivityTestRequest request = req.GetData<ProviderConnectivityTestRequest>();
            if (request == null || request.Provider == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Provider settings are required.");
            }

            ModelProviderSettings provider = await ResolveProviderForTestAsync(request.Provider, req.CancellationToken).ConfigureAwait(false);
            return await TestProviderAsync(provider, req.CancellationToken).ConfigureAwait(false);
        }

        private async Task<ModelProviderSettings> ResolveProviderForTestAsync(ModelProviderSettings provider, CancellationToken token)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (!String.IsNullOrWhiteSpace(provider.ApiKey) || String.IsNullOrWhiteSpace(provider.Id)) return provider;

            ModelProviderSettings existing = await _Persistence.ModelProviders.ReadAsync(provider.Id, token).ConfigureAwait(false);
            if (existing == null || String.IsNullOrWhiteSpace(existing.ApiKey)) return provider;

            provider.ApiKey = existing.ApiKey;
            return provider;
        }

        private async Task<ProviderConnectivityTestResponse> TestProviderAsync(ModelProviderSettings provider, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ProviderConnectivityTestResponse result = new ProviderConnectivityTestResponse
            {
                ProviderId = provider.Id,
                Model = provider.Model
            };

            try
            {
                if (String.IsNullOrWhiteSpace(provider.Endpoint))
                    throw new ArgumentException("Provider endpoint is required.");
                if (String.IsNullOrWhiteSpace(provider.Model))
                    throw new ArgumentException("Provider model is required.");

                using CompletionClientBase client = CreateClient(provider);
                ChatCompletionOptions options = new ChatCompletionOptions
                {
                    SystemPrompt = "Reply with the single word OK."
                };
                ChatResponse response = await client.ChatAsync("Reply with the single word OK.", options, token).ConfigureAwait(false);
                stopwatch.Stop();

                result.Success = response.Success;
                result.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Message = response.Success ? "Provider returned a response." : null;
                result.Error = response.Success ? null : Sanitize(response.Error ?? "Provider test failed.", provider.ApiKey);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Error = Sanitize(ex.Message, provider.ApiKey);
                return result;
            }
        }

        private CompletionClientBase CreateClient(ModelProviderSettings provider)
        {
            CompletionClientBase client;
            if (provider.Type == ModelProviderTypeEnum.Gemini)
                client = new GeminiClient(provider.Endpoint, provider.ApiKey, _Logging);
            else if (provider.Type == ModelProviderTypeEnum.Ollama)
                client = new OllamaClient(provider.Endpoint, provider.ApiKey, _Logging);
            else
                client = new OpenAiClient(provider.Endpoint, provider.ApiKey, _Logging);

            client.Model = provider.Model;
            client.TimeoutMs = provider.RequestTimeoutMs;
            if (provider.Temperature.HasValue) client.Temperature = provider.Temperature.Value;
            if (provider.TopP.HasValue) client.TopP = provider.TopP.Value;
            if (provider.MaxTokens.HasValue) client.MaxTokens = provider.MaxTokens.Value;
            return client;
        }

        private ModelProviderRead ToRead(ModelProviderSettings provider)
        {
            ModelProviderSettings.ApplyHealthCheckDefaults(provider);
            return new ModelProviderRead
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                Endpoint = provider.Endpoint,
                ApiKey = null,
                HasApiKey = !String.IsNullOrEmpty(provider.ApiKey),
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
                HealthCheckUseAuth = provider.HealthCheckUseAuth,
                Health = _HealthChecks?.GetHealthStatus(provider.Id) ?? CreateInitialHealthStatus(provider)
            };
        }

        private static ModelProviderSettings ToSettings(ModelProviderUpdate request)
        {
            return new ModelProviderSettings
            {
                Id = request.Id,
                Name = request.Name,
                Type = request.Type,
                Endpoint = request.Endpoint,
                ApiKey = request.ClearApiKey ? null : request.ApiKey,
                Model = request.Model,
                SystemPrompt = request.SystemPrompt,
                Enabled = request.Enabled,
                DefaultStreaming = request.DefaultStreaming,
                SupportsNativeToolCalls = request.SupportsNativeToolCalls,
                UseNativeToolCalls = request.UseNativeToolCalls,
                SupportsStrictJson = request.SupportsStrictJson,
                ToolCapabilityNote = request.ToolCapabilityNote,
                Temperature = request.Temperature,
                TopP = request.TopP,
                MaxTokens = request.MaxTokens,
                RequestTimeoutMs = request.RequestTimeoutMs,
                MaxConcurrentRequests = request.MaxConcurrentRequests,
                HealthCheckEnabled = request.HealthCheckEnabled,
                HealthCheckUrl = request.HealthCheckUrl,
                HealthCheckMethod = request.HealthCheckMethod,
                HealthCheckIntervalMs = request.HealthCheckIntervalMs,
                HealthCheckTimeoutMs = request.HealthCheckTimeoutMs,
                HealthCheckExpectedStatusCode = request.HealthCheckExpectedStatusCode,
                HealthyThreshold = request.HealthyThreshold,
                UnhealthyThreshold = request.UnhealthyThreshold,
                HealthCheckUseAuth = request.HealthCheckUseAuth
            };
        }

        private ModelProviderSummary ToSummary(ModelProviderSettings provider)
        {
            ModelProviderSummary summary = ModelProviderSummary.From(provider);
            if (summary != null)
                summary.Health = _HealthChecks?.GetHealthStatus(provider.Id) ?? CreateInitialHealthStatus(provider);
            return summary;
        }

        private static EndpointHealthStatus CreateInitialHealthStatus(ModelProviderSettings provider)
        {
            if (provider == null) return null;
            ModelProviderSettings.ApplyHealthCheckDefaults(provider);

            return EndpointHealthStatus.FromState(new EndpointHealthState
            {
                EndpointId = provider.Id,
                EndpointName = provider.Name ?? provider.Id,
                HealthCheckEnabled = provider.Enabled && provider.HealthCheckEnabled,
                IsHealthy = true
            });
        }

        private async Task RepairDefaultProviderIdAsync(CancellationToken token)
        {
            TablixSettings settings = _SettingsManager.Settings;
            ModelProviderSettings currentDefault = null;
            if (!String.IsNullOrWhiteSpace(settings.Chat.DefaultProviderId))
                currentDefault = await _Persistence.ModelProviders.ReadAsync(settings.Chat.DefaultProviderId, token).ConfigureAwait(false);

            if (currentDefault != null && currentDefault.Enabled)
                return;

            List<ModelProviderSettings> enabledProviders = await _Persistence.ModelProviders.EnumerateAsync(1000, 0, null, true, token).ConfigureAwait(false);
            string replacementProviderId = enabledProviders.Count > 0 ? enabledProviders[0].Id : null;
            if (String.Equals(settings.Chat.DefaultProviderId, replacementProviderId, StringComparison.OrdinalIgnoreCase))
                return;

            settings.Chat.DefaultProviderId = replacementProviderId;
            _SettingsManager.UpdateSettings(settings);
        }

        private static void ReadEnumerationQuery(ApiRequest req, out int maxResults, out int skip, out string filter, out bool? enabled)
        {
            maxResults = 100;
            skip = 0;
            filter = req.Http.Request.Query.Elements.Get("filter");
            enabled = null;

            string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
            if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int parsedMax))
                maxResults = Math.Clamp(parsedMax, 1, 1000);

            string skipStr = req.Http.Request.Query.Elements.Get("skip");
            if (!String.IsNullOrEmpty(skipStr) && Int32.TryParse(skipStr, out int parsedSkip))
                skip = Math.Max(parsedSkip, 0);

            string enabledStr = req.Http.Request.Query.Elements.Get("enabled");
            if (!String.IsNullOrEmpty(enabledStr) && Boolean.TryParse(enabledStr, out bool parsedEnabled))
                enabled = parsedEnabled;
        }

        private static string Sanitize(string message, string apiKey)
        {
            string sanitized = message ?? String.Empty;
            if (!String.IsNullOrEmpty(apiKey))
                sanitized = sanitized.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
            return sanitized;
        }
    }
}
