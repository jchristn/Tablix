namespace Tablix.Core.Models
{
    /// <summary>
    /// Prepared prompt preview for a database chat request.
    /// </summary>
    public class ChatPromptPreviewResponse
    {
        #region Public-Members

        /// <summary>
        /// Whether the preview was prepared.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Model provider identifier.
        /// </summary>
        public string ProviderId { get; set; } = null;

        /// <summary>
        /// Model configured for the provider.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Effective system prompt sent to the provider.
        /// </summary>
        public string SystemPrompt { get; set; } = null;

        /// <summary>
        /// Database context prompt sent with the conversation.
        /// </summary>
        public string ContextPrompt { get; set; } = null;

        /// <summary>
        /// System prompt character count.
        /// </summary>
        public int SystemPromptCharacters { get; set; } = 0;

        /// <summary>
        /// Database context prompt character count.
        /// </summary>
        public int ContextPromptCharacters { get; set; } = 0;

        /// <summary>
        /// Estimated system prompt tokens.
        /// </summary>
        public int SystemPromptEstimatedTokens { get; set; } = 0;

        /// <summary>
        /// Estimated database context prompt tokens.
        /// </summary>
        public int ContextPromptEstimatedTokens { get; set; } = 0;

        /// <summary>
        /// Conversation messages included in the context prompt.
        /// </summary>
        public int ConversationMessages { get; set; } = 0;

        /// <summary>
        /// Error message when unsuccessful.
        /// </summary>
        public string Error { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatPromptPreviewResponse()
        {
        }

        #endregion
    }
}
