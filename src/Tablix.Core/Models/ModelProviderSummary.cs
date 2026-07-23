namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;
    using Tablix.Core.Settings;

    /// <summary>
    /// Redacted model provider summary.
    /// </summary>
    public class ModelProviderSummary
    {
        #region Public-Members

        /// <summary>
        /// Provider identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Provider display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Provider type.
        /// </summary>
        public ModelProviderTypeEnum Type { get; set; } = ModelProviderTypeEnum.Ollama;

        /// <summary>
        /// Provider endpoint.
        /// </summary>
        public string Endpoint { get; set; } = null;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Whether the provider is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default streaming setting.
        /// </summary>
        public bool DefaultStreaming { get; set; } = true;

        /// <summary>
        /// Whether an API key is configured.
        /// </summary>
        public bool HasApiKey { get; set; } = false;

        /// <summary>
        /// Whether this provider/model is expected to support native PolyPrompt tool calls.
        /// </summary>
        public bool SupportsNativeToolCalls { get; set; } = false;

        /// <summary>
        /// Whether native tool calls are enabled for this provider.
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
        /// Maximum concurrent provider requests for batch operations.
        /// </summary>
        public int MaxConcurrentRequests { get; set; } = 1;

        /// <summary>
        /// Whether provider health checks are enabled.
        /// </summary>
        public bool HealthCheckEnabled { get; set; } = true;

        /// <summary>
        /// Health check URL.
        /// </summary>
        public string HealthCheckUrl { get; set; } = null;

        /// <summary>
        /// Health check HTTP method.
        /// </summary>
        public HealthCheckMethodEnum HealthCheckMethod { get; set; } = HealthCheckMethodEnum.GET;

        /// <summary>
        /// Health check interval in milliseconds.
        /// </summary>
        public int HealthCheckIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Health check timeout in milliseconds.
        /// </summary>
        public int HealthCheckTimeoutMs { get; set; } = 2000;

        /// <summary>
        /// Expected success status code.
        /// </summary>
        public int HealthCheckExpectedStatusCode { get; set; } = 200;

        /// <summary>
        /// Consecutive successes required to mark healthy.
        /// </summary>
        public int HealthyThreshold { get; set; } = 2;

        /// <summary>
        /// Consecutive failures required to mark unhealthy.
        /// </summary>
        public int UnhealthyThreshold { get; set; } = 2;

        /// <summary>
        /// Whether the API key should be sent with health checks.
        /// </summary>
        public bool HealthCheckUseAuth { get; set; } = false;

        /// <summary>
        /// Current health snapshot.
        /// </summary>
        public EndpointHealthStatus Health { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelProviderSummary()
        {
        }

        /// <summary>
        /// Create from settings.
        /// </summary>
        /// <param name="provider">Provider settings.</param>
        /// <returns>Summary.</returns>
        public static ModelProviderSummary From(ModelProviderSettings provider)
        {
            if (provider == null) return null;
            ModelProviderSettings.ApplyHealthCheckDefaults(provider);

            return new ModelProviderSummary
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                Endpoint = provider.Endpoint,
                Model = provider.Model,
                Enabled = provider.Enabled,
                DefaultStreaming = provider.DefaultStreaming,
                HasApiKey = !System.String.IsNullOrEmpty(provider.ApiKey),
                SupportsNativeToolCalls = provider.SupportsNativeToolCalls,
                UseNativeToolCalls = provider.UseNativeToolCalls,
                SupportsStrictJson = provider.SupportsStrictJson,
                ToolCapabilityNote = provider.ToolCapabilityNote,
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

        #endregion
    }
}
