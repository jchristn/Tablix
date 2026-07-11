namespace Tablix.Core.Models
{
    /// <summary>
    /// MCP error response.
    /// </summary>
    public class McpErrorResponse
    {
        /// <summary>
        /// Whether the operation succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Database identifier, when applicable.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpErrorResponse()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="error">Error message.</param>
        public McpErrorResponse(string error)
        {
            Error = error;
        }
    }
}
