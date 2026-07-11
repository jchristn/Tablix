namespace Tablix.Core.Models
{
    /// <summary>
    /// Chat conversation message.
    /// </summary>
    public class ChatMessage
    {
        #region Public-Members

        /// <summary>
        /// Message role: user, assistant, or system.
        /// </summary>
        public string Role { get; set; } = null;

        /// <summary>
        /// Message content.
        /// </summary>
        public string Content { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatMessage()
        {
        }

        #endregion
    }
}
