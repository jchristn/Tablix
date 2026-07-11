namespace Tablix.Server.Handlers
{
    /// <summary>
    /// Model-visible tool result envelope.
    /// </summary>
    internal class ToolResultEnvelope
    {
        /// <summary>
        /// Whether the tool succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Serialized result payload.
        /// </summary>
        public string Result { get; set; } = null;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ToolResultEnvelope()
        {
        }
    }
}
