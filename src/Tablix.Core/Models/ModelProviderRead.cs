namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Redacted model provider settings for dashboard reads.
    /// </summary>
    public class ModelProviderRead
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
        /// Redacted API key placeholder; read responses do not include the secret value.
        /// </summary>
        public string ApiKey { get; set; } = null;

        /// <summary>
        /// Whether an API key is configured.
        /// </summary>
        public bool HasApiKey { get; set; } = false;

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

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelProviderRead()
        {
        }

        #endregion
    }
}
