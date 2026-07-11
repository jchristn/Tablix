namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// MCP response item for a database or table context update.
    /// </summary>
    public class McpContextUpdateItemResponse
    {
        /// <summary>
        /// Whether this update succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Context scope.
        /// </summary>
        public ContextScopeEnum Scope { get; set; } = ContextScopeEnum.Database;

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
        /// Saved context text.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Update mode.
        /// </summary>
        public string Mode { get; set; } = null;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpContextUpdateItemResponse()
        {
        }
    }
}
