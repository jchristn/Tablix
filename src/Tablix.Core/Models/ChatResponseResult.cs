namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Response from a non-streaming database chat request.
    /// </summary>
    public class ChatResponseResult
    {
        #region Public-Members

        /// <summary>
        /// Whether the request succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Provider identifier.
        /// </summary>
        public string ProviderId { get; set; } = null;

        /// <summary>
        /// Model used by the provider.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Assistant response content.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Telemetry for the assistant response.
        /// </summary>
        public ChatTelemetry Telemetry { get; set; } = null;

        /// <summary>
        /// Tool calls made while producing the assistant response.
        /// </summary>
        public List<ChatToolCall> ToolCalls
        {
            get { return _ToolCalls; }
            set { _ToolCalls = value ?? new List<ChatToolCall>(); }
        }

        /// <summary>
        /// Error message when unsuccessful.
        /// </summary>
        public string Error { get; set; } = null;

        #endregion

        #region Private-Members

        private List<ChatToolCall> _ToolCalls = new List<ChatToolCall>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatResponseResult()
        {
        }

        #endregion
    }
}
