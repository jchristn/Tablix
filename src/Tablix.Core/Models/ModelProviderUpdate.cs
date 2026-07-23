namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Model provider update request.
    /// </summary>
    public class ModelProviderUpdate
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
        /// New provider API key. Leave empty to preserve the existing key.
        /// </summary>
        public string ApiKey { get; set; } = null;

        /// <summary>
        /// Clear the existing provider API key.
        /// </summary>
        public bool ClearApiKey { get; set; } = false;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Provider-specific system prompt override.
        /// </summary>
        public string SystemPrompt { get; set; } = null;

        /// <summary>
        /// Whether the provider is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether streaming is enabled by default.
        /// </summary>
        public bool DefaultStreaming { get; set; } = true;

        /// <summary>
        /// Whether the provider/model supports native tool calls.
        /// </summary>
        public bool SupportsNativeToolCalls { get; set; } = false;

        /// <summary>
        /// Whether native tool calls should be used.
        /// </summary>
        public bool UseNativeToolCalls { get; set; } = false;

        /// <summary>
        /// Whether the provider/model supports strict JSON responses.
        /// </summary>
        public bool SupportsStrictJson { get; set; } = false;

        /// <summary>
        /// Human-readable tool capability note.
        /// </summary>
        public string ToolCapabilityNote { get; set; } = null;

        /// <summary>
        /// Sampling temperature.
        /// </summary>
        public double? Temperature { get; set; } = null;

        /// <summary>
        /// Top-p sampling value.
        /// </summary>
        public double? TopP { get; set; } = null;

        /// <summary>
        /// Maximum output tokens.
        /// </summary>
        public int? MaxTokens { get; set; } = null;

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 120000;

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

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelProviderUpdate()
        {
        }

        #endregion
    }
}
