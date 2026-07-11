namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// MCP request item for a database or table context update.
    /// </summary>
    public class McpContextUpdateItemRequest
    {
        /// <summary>
        /// Context scope for this item.
        /// </summary>
        public ContextScopeEnum? Scope { get; set; } = null;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Table metadata identifier for table-scoped context.
        /// </summary>
        public string TableId { get; set; } = null;

        /// <summary>
        /// Table name for table-scoped context.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Context text.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Update mode: replace or append.
        /// </summary>
        public string Mode { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpContextUpdateItemRequest()
        {
        }
    }
}
