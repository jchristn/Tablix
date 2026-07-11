namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using Tablix.Core.Enums;

    /// <summary>
    /// MCP request for updating persisted database or table context.
    /// </summary>
    public class McpUpdateContextRequest
    {
        /// <summary>
        /// Context scope. Defaults to database scope for backward compatibility.
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
        /// Update mode.
        /// </summary>
        public string Mode { get; set; } = "replace";

        /// <summary>
        /// Multiple context updates. When supplied, item fields override top-level defaults.
        /// </summary>
        public List<McpContextUpdateItemRequest> Updates
        {
            get { return _Updates; }
            set { _Updates = value ?? new List<McpContextUpdateItemRequest>(); }
        }

        private List<McpContextUpdateItemRequest> _Updates = new List<McpContextUpdateItemRequest>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpUpdateContextRequest()
        {
        }
    }
}
