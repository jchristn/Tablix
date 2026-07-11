namespace Tablix.Core.Models
{
    /// <summary>
    /// MCP request for executing a SQL query.
    /// </summary>
    public class McpExecuteQueryRequest
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// SQL query.
        /// </summary>
        public string Query { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpExecuteQueryRequest()
        {
        }
    }
}
