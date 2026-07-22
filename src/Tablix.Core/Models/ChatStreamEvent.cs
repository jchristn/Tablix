namespace Tablix.Core.Models
{
    /// <summary>
    /// Server-sent event payload for streaming chat responses.
    /// </summary>
    public class ChatStreamEvent
    {
        #region Public-Members

        /// <summary>
        /// Event type: started, token, completed, or error.
        /// </summary>
        public string EventType { get; set; } = null;

        /// <summary>
        /// Text delta for token events.
        /// </summary>
        public string Delta { get; set; } = null;

        /// <summary>
        /// Full assistant message for completed events.
        /// </summary>
        public string Message { get; set; } = null;

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
        /// Telemetry for completed events.
        /// </summary>
        public ChatTelemetry Telemetry { get; set; } = null;

        /// <summary>
        /// Tool call details for tool_started and tool_completed events.
        /// </summary>
        public ChatToolCall ToolCall { get; set; } = null;

        /// <summary>
        /// Verification metadata for completed events.
        /// </summary>
        public VerifiedAnswer VerifiedAnswer { get; set; } = null;

        /// <summary>
        /// Ambiguity signals detected before answer generation.
        /// </summary>
        public System.Collections.Generic.List<AmbiguitySignal> Ambiguities
        {
            get { return _Ambiguities; }
            set { _Ambiguities = value ?? new System.Collections.Generic.List<AmbiguitySignal>(); }
        }

        /// <summary>
        /// Execution path used for this response.
        /// </summary>
        public string ExecutionPath { get; set; } = null;

        /// <summary>
        /// Provider/tool capability notice for the UI.
        /// </summary>
        public string CapabilityNotice { get; set; } = null;

        /// <summary>
        /// Whether this event terminates the stream.
        /// </summary>
        public bool Done { get; set; } = false;

        /// <summary>
        /// Error message when EventType is error.
        /// </summary>
        public string Error { get; set; } = null;

        #endregion

        #region Private-Members

        private System.Collections.Generic.List<AmbiguitySignal> _Ambiguities = new System.Collections.Generic.List<AmbiguitySignal>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatStreamEvent()
        {
        }

        #endregion
    }
}
