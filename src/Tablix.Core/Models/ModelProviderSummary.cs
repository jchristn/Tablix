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
                ToolCapabilityNote = provider.ToolCapabilityNote
            };
        }

        #endregion
    }
}
