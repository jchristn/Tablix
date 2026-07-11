namespace Tablix.Core.Models
{
    /// <summary>
    /// Chat-visible tool call details.
    /// </summary>
    public class ChatToolCall
    {
        #region Public-Members

        /// <summary>
        /// Tool call identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Tool name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Tool arguments as compact JSON.
        /// </summary>
        public string Arguments { get; set; } = null;

        /// <summary>
        /// Tool result as compact JSON.
        /// </summary>
        public string Result { get; set; } = null;

        /// <summary>
        /// Tool error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Whether the tool call succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Tool runtime in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatToolCall()
        {
        }

        #endregion
    }
}
