namespace Tablix.Core.Settings
{
    using System;
    using Tablix.Core.Enums;

    /// <summary>
    /// Configuration for a model endpoint used by Tablix chat.
    /// </summary>
    public class ModelProviderSettings
    {
        #region Public-Members

        /// <summary>
        /// Unique provider identifier.
        /// </summary>
        public string Id
        {
            get { return _Id; }
            set { _Id = String.IsNullOrWhiteSpace(value) ? "provider_" + Guid.NewGuid().ToString().Substring(0, 8) : value; }
        }

        /// <summary>
        /// Human-readable provider name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Provider API type.
        /// </summary>
        public ModelProviderTypeEnum Type { get; set; } = ModelProviderTypeEnum.Ollama;

        /// <summary>
        /// Base endpoint URL for the provider.
        /// </summary>
        public string Endpoint { get; set; } = null;

        /// <summary>
        /// Optional API key. This value is secret-bearing and must be redacted from API responses.
        /// </summary>
        public string ApiKey { get; set; } = null;

        /// <summary>
        /// Default model name.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Optional provider-specific system prompt.
        /// </summary>
        public string SystemPrompt { get; set; } = null;

        /// <summary>
        /// Whether this provider is available for chat.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether chat should stream responses by default for this provider.
        /// </summary>
        public bool DefaultStreaming { get; set; } = true;

        /// <summary>
        /// Whether this provider/model is expected to support native PolyPrompt tool calls.
        /// </summary>
        public bool SupportsNativeToolCalls { get; set; } = false;

        /// <summary>
        /// Whether Tablix should attempt native PolyPrompt tool calls for this provider.
        /// </summary>
        public bool UseNativeToolCalls { get; set; } = false;

        /// <summary>
        /// Whether this provider/model is expected to reliably return strict JSON for fallback planning.
        /// </summary>
        public bool SupportsStrictJson { get; set; } = false;

        /// <summary>
        /// Human-readable note describing provider/model tool capability.
        /// </summary>
        public string ToolCapabilityNote { get; set; } = null;

        /// <summary>
        /// Sampling temperature. Null uses the provider/client default; non-null values are clamped from 0.0 to 2.0.
        /// </summary>
        public double? Temperature
        {
            get { return _Temperature; }
            set { _Temperature = value.HasValue ? Math.Clamp(value.Value, 0.0, 2.0) : (double?)null; }
        }

        /// <summary>
        /// Nucleus sampling value. Null uses the provider/client default; non-null values are clamped from 0.0 to 1.0.
        /// </summary>
        public double? TopP
        {
            get { return _TopP; }
            set { _TopP = value.HasValue ? Math.Clamp(value.Value, 0.0, 1.0) : (double?)null; }
        }

        /// <summary>
        /// Maximum output tokens. Null uses the provider/client default; non-null values are clamped from 1 to 10000000.
        /// </summary>
        public int? MaxTokens
        {
            get { return _MaxTokens; }
            set { _MaxTokens = value.HasValue ? Math.Clamp(value.Value, 1, 10000000) : (int?)null; }
        }

        /// <summary>
        /// Request timeout in milliseconds. Values are clamped from 1000 to 600000.
        /// </summary>
        public int RequestTimeoutMs
        {
            get { return _RequestTimeoutMs; }
            set { _RequestTimeoutMs = Math.Clamp(value, 1000, 600000); }
        }

        /// <summary>
        /// Maximum concurrent provider requests Tablix may issue for batch operations. Values are clamped from 1 to 16.
        /// </summary>
        public int MaxConcurrentRequests
        {
            get { return _MaxConcurrentRequests; }
            set { _MaxConcurrentRequests = Math.Clamp(value, 1, 16); }
        }

        /// <summary>
        /// Whether model provider health checks are enabled.
        /// </summary>
        public bool HealthCheckEnabled { get; set; } = true;

        /// <summary>
        /// Health check URL. When empty, a provider-specific default is derived from the endpoint.
        /// </summary>
        public string HealthCheckUrl { get; set; } = null;

        /// <summary>
        /// Health check HTTP method.
        /// </summary>
        public HealthCheckMethodEnum HealthCheckMethod { get; set; } = HealthCheckMethodEnum.GET;

        /// <summary>
        /// Health check interval in milliseconds. Values are clamped from 1000 to 3600000.
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get { return _HealthCheckIntervalMs; }
            set { _HealthCheckIntervalMs = value <= 0 ? 5000 : Math.Clamp(value, 1000, 3600000); }
        }

        /// <summary>
        /// Health check timeout in milliseconds. Values are clamped from 10 to 600000.
        /// </summary>
        public int HealthCheckTimeoutMs
        {
            get { return _HealthCheckTimeoutMs; }
            set { _HealthCheckTimeoutMs = value <= 0 ? 2000 : Math.Clamp(value, 10, 600000); }
        }

        /// <summary>
        /// Expected HTTP status code for a successful health check.
        /// </summary>
        public int HealthCheckExpectedStatusCode
        {
            get { return _HealthCheckExpectedStatusCode; }
            set { _HealthCheckExpectedStatusCode = value <= 0 ? 200 : Math.Clamp(value, 100, 599); }
        }

        /// <summary>
        /// Consecutive successful checks required to mark the provider healthy. Values are clamped from 1 to 100.
        /// </summary>
        public int HealthyThreshold
        {
            get { return _HealthyThreshold; }
            set { _HealthyThreshold = value <= 0 ? 2 : Math.Clamp(value, 1, 100); }
        }

        /// <summary>
        /// Consecutive failed checks required to mark the provider unhealthy. Values are clamped from 1 to 100.
        /// </summary>
        public int UnhealthyThreshold
        {
            get { return _UnhealthyThreshold; }
            set { _UnhealthyThreshold = value <= 0 ? 2 : Math.Clamp(value, 1, 100); }
        }

        /// <summary>
        /// Whether the provider API key should be sent with health check requests.
        /// </summary>
        public bool HealthCheckUseAuth { get; set; } = false;

        #endregion

        #region Private-Members

        private string _Id = "provider_" + Guid.NewGuid().ToString().Substring(0, 8);
        private double? _Temperature = 0.2;
        private double? _TopP = null;
        private int? _MaxTokens = 4096;
        private int _RequestTimeoutMs = 120000;
        private int _MaxConcurrentRequests = 1;
        private int _HealthCheckIntervalMs = 5000;
        private int _HealthCheckTimeoutMs = 2000;
        private int _HealthCheckExpectedStatusCode = 200;
        private int _HealthyThreshold = 2;
        private int _UnhealthyThreshold = 2;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelProviderSettings()
        {
        }

        /// <summary>
        /// Apply provider-specific health check defaults.
        /// </summary>
        /// <param name="provider">Provider settings.</param>
        public static void ApplyHealthCheckDefaults(ModelProviderSettings provider)
        {
            if (provider == null) return;

            provider.HealthCheckIntervalMs = provider.HealthCheckIntervalMs;
            provider.HealthCheckTimeoutMs = provider.HealthCheckTimeoutMs;
            provider.HealthCheckExpectedStatusCode = provider.HealthCheckExpectedStatusCode;
            provider.HealthyThreshold = provider.HealthyThreshold;
            provider.UnhealthyThreshold = provider.UnhealthyThreshold;

            if (String.IsNullOrWhiteSpace(provider.HealthCheckUrl))
                provider.HealthCheckUrl = BuildDefaultHealthCheckUrl(provider);

        }

        /// <summary>
        /// Build the default health check URL for a provider.
        /// </summary>
        /// <param name="provider">Provider settings.</param>
        /// <returns>Default health check URL.</returns>
        public static string BuildDefaultHealthCheckUrl(ModelProviderSettings provider)
        {
            if (provider == null || String.IsNullOrWhiteSpace(provider.Endpoint)) return null;

            string endpoint = provider.Endpoint.TrimEnd('/');
            if (provider.Type == ModelProviderTypeEnum.Ollama)
            {
                if (endpoint.EndsWith("/api/tags", StringComparison.OrdinalIgnoreCase)) return endpoint;
                if (endpoint.EndsWith("/api", StringComparison.OrdinalIgnoreCase)) return endpoint + "/tags";
                return endpoint + "/api/tags";
            }

            if (provider.Type == ModelProviderTypeEnum.Gemini)
            {
                if (endpoint.EndsWith("/models", StringComparison.OrdinalIgnoreCase)) return endpoint;
                if (endpoint.EndsWith("/v1beta", StringComparison.OrdinalIgnoreCase)) return endpoint + "/models";
                return endpoint + "/v1beta/models";
            }

            if (endpoint.EndsWith("/models", StringComparison.OrdinalIgnoreCase)) return endpoint;
            if (endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return endpoint + "/models";
            return endpoint + "/v1/models";
        }

        #endregion
    }
}
