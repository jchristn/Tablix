namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Redacted model provider settings for dashboard reads.
    /// </summary>
    public class ModelProviderRead
    {
        #region Public-Members

        public string Id { get; set; } = null;
        public string Name { get; set; } = null;
        public ModelProviderTypeEnum Type { get; set; } = ModelProviderTypeEnum.Ollama;
        public string Endpoint { get; set; } = null;
        public string ApiKey { get; set; } = null;
        public bool HasApiKey { get; set; } = false;
        public string Model { get; set; } = null;
        public string SystemPrompt { get; set; } = null;
        public bool Enabled { get; set; } = true;
        public bool DefaultStreaming { get; set; } = true;
        public double? Temperature { get; set; } = null;
        public double? TopP { get; set; } = null;
        public int? MaxTokens { get; set; } = null;
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
