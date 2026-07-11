namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Request to chat with a selected database through a configured provider.
    /// </summary>
    public class ChatRequest
    {
        #region Public-Members

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Model provider identifier.
        /// </summary>
        public string ProviderId { get; set; } = null;

        /// <summary>
        /// Conversation messages.
        /// </summary>
        public List<ChatMessage> Messages
        {
            get { return _Messages; }
            set { _Messages = value ?? new List<ChatMessage>(); }
        }

        /// <summary>
        /// Optional streaming preference. Null uses provider or chat defaults.
        /// </summary>
        public bool? Streaming { get; set; } = null;

        /// <summary>
        /// Optional per-request override to prefer native provider tool calls.
        /// </summary>
        public bool? PreferNativeToolCalls { get; set; } = null;

        /// <summary>
        /// Optional per-request override to use fallback execution when native tools are unavailable or omitted.
        /// </summary>
        public bool? FallbackWhenNativeToolNotCalled { get; set; } = null;

        #endregion

        #region Private-Members

        private List<ChatMessage> _Messages = new List<ChatMessage>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatRequest()
        {
        }

        #endregion
    }
}
