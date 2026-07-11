namespace Tablix.Core.Models
{
    /// <summary>
    /// MCP request for discovering one table.
    /// </summary>
    public class McpDiscoverTableRequest
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Table name.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpDiscoverTableRequest()
        {
        }
    }
}
