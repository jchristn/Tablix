namespace Tablix.Server.Handlers
{
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// Prepared chat request state.
    /// </summary>
    internal class ChatPreparation
    {
        /// <summary>
        /// Selected database.
        /// </summary>
        public DatabaseEntry Database { get; set; } = null;

        /// <summary>
        /// Selected model provider.
        /// </summary>
        public ModelProviderSettings Provider { get; set; } = null;

        /// <summary>
        /// Cached or newly crawled database detail.
        /// </summary>
        public DatabaseDetail Detail { get; set; } = null;

        /// <summary>
        /// Current settings.
        /// </summary>
        public TablixSettings Settings { get; set; } = null;

        /// <summary>
        /// System prompt.
        /// </summary>
        public string SystemPrompt { get; set; } = null;

        /// <summary>
        /// Full user prompt with database context.
        /// </summary>
        public string Prompt { get; set; } = null;

        /// <summary>
        /// Error response when preparation fails.
        /// </summary>
        public object Error { get; set; } = null;

        /// <summary>
        /// Create a failed preparation.
        /// </summary>
        /// <param name="error">Error response.</param>
        /// <returns>Failed preparation.</returns>
        public static ChatPreparation Fail(object error)
        {
            return new ChatPreparation { Error = error };
        }
    }
}
