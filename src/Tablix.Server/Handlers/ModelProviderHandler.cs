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
    using SwiftStack.Rest;
    using SyslogLogging;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence;
    using Tablix.Core.Settings;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;

    /// <summary>
    /// REST handlers for persisted model provider management.
    /// </summary>
    public class ModelProviderHandler
    {
        private readonly DatabaseDriverBase _Persistence;
        private readonly LoggingModule _Logging;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="logging">Logging module.</param>
        public ModelProviderHandler(DatabaseDriverBase persistence, LoggingModule logging)
        {
            _Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _Logging = logging ?? new LoggingModule();
        }

        /// <summary>
        /// List model providers.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Paginated provider summaries.</returns>
        public async Task<object> ListProvidersAsync(AppRequest req)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ReadEnumerationQuery(req, out int maxResults, out int skip, out string filter, out bool? enabled);

            long totalRecords = await _Persistence.ModelProviders.CountAsync(filter, enabled, req.CancellationToken).ConfigureAwait(false);
            List<ModelProviderSettings> providers = await _Persistence.ModelProviders.EnumerateAsync(maxResults, skip, filter, enabled, req.CancellationToken).ConfigureAwait(false);
            List<ModelProviderSummary> summaries = providers.Select(ModelProviderSummary.From).Where(summary => summary != null).ToList();
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
        public async Task<object> GetProviderAsync(AppRequest req)
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
        /// Create a model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Created provider summary.</returns>
        public async Task<object> AddProviderAsync(AppRequest req)
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
                req.Http.Response.StatusCode = 201;
                return ModelProviderSummary.From(created);
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
        public async Task<object> UpdateProviderAsync(AppRequest req)
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
            return ModelProviderSummary.From(updated);
        }

        /// <summary>
        /// Delete a model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Null response.</returns>
        public async Task<object> DeleteProviderAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            bool deleted = await _Persistence.ModelProviders.DeleteAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (!deleted)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + id + "' not found.");
            }

            req.Http.Response.StatusCode = 204;
            return null;
        }

        /// <summary>
        /// Test a saved model provider.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Connectivity test result.</returns>
        public async Task<object> TestSavedProviderAsync(AppRequest req)
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
        public async Task<object> TestProviderRequestAsync(AppRequest req)
        {
            ProviderConnectivityTestRequest request = req.GetData<ProviderConnectivityTestRequest>();
            if (request == null || request.Provider == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Provider settings are required.");
            }

            return await TestProviderAsync(request.Provider, req.CancellationToken).ConfigureAwait(false);
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
            client.TimeoutMs = Math.Clamp(provider.RequestTimeoutMs, 1000, 60000);
            if (provider.Temperature.HasValue) client.Temperature = provider.Temperature.Value;
            if (provider.TopP.HasValue) client.TopP = provider.TopP.Value;
            if (provider.MaxTokens.HasValue) client.MaxTokens = provider.MaxTokens.Value;
            return client;
        }

        private static ModelProviderRead ToRead(ModelProviderSettings provider)
        {
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
                RequestTimeoutMs = provider.RequestTimeoutMs
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
                RequestTimeoutMs = request.RequestTimeoutMs
            };
        }

        private static void ReadEnumerationQuery(AppRequest req, out int maxResults, out int skip, out string filter, out bool? enabled)
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
