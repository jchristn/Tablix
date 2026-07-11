namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Supported model provider types for Tablix chat.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ModelProviderTypeEnum
    {
        /// <summary>
        /// OpenAI API.
        /// </summary>
        OpenAI,

        /// <summary>
        /// OpenAI-compatible API.
        /// </summary>
        OpenAICompatible,

        /// <summary>
        /// Google Gemini API.
        /// </summary>
        Gemini,

        /// <summary>
        /// Ollama API.
        /// </summary>
        Ollama
    }
}
