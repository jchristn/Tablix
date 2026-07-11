namespace Tablix.Core.Models
{
    /// <summary>
    /// MCP response for table discovery.
    /// </summary>
    public class McpTableDetailResponse
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Saved database context.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Table detail.
        /// </summary>
        public TableDetail Table { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpTableDetailResponse()
        {
        }
    }
}
