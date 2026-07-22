namespace Tablix.Server.Handlers
{
    using System.Collections.Generic;
    using Tablix.Core.Models;

    /// <summary>
    /// Internal chat execution result.
    /// </summary>
    internal class ChatExecutionResult
    {
        /// <summary>
        /// Whether execution succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Assistant message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Execution path.
        /// </summary>
        public string ExecutionPath { get; set; } = null;

        /// <summary>
        /// Capability notice.
        /// </summary>
        public string CapabilityNotice { get; set; } = null;

        /// <summary>
        /// Telemetry.
        /// </summary>
        public ChatTelemetry Telemetry { get; set; } = null;

        /// <summary>
        /// Verification metadata.
        /// </summary>
        public VerifiedAnswer VerifiedAnswer { get; set; } = null;

        /// <summary>
        /// Ambiguity signals.
        /// </summary>
        public List<AmbiguitySignal> Ambiguities
        {
            get { return _Ambiguities; }
            set { _Ambiguities = value ?? new List<AmbiguitySignal>(); }
        }

        /// <summary>
        /// Tool calls.
        /// </summary>
        public List<ChatToolCall> ToolCalls
        {
            get { return _ToolCalls; }
            set { _ToolCalls = value ?? new List<ChatToolCall>(); }
        }

        private List<ChatToolCall> _ToolCalls = new List<ChatToolCall>();
        private List<AmbiguitySignal> _Ambiguities = new List<AmbiguitySignal>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatExecutionResult()
        {
        }
    }
}
