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

        #endregion

        #region Private-Members

        private string _Id = "provider_" + Guid.NewGuid().ToString().Substring(0, 8);
        private double? _Temperature = 0.2;
        private double? _TopP = null;
        private int? _MaxTokens = 4096;
        private int _RequestTimeoutMs = 120000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ModelProviderSettings()
        {
        }

        #endregion
    }
}
