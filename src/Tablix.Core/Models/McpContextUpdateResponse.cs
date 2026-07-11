namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using Tablix.Core.Enums;

    /// <summary>
    /// MCP context update response.
    /// </summary>
    public class McpContextUpdateResponse
    {
        /// <summary>
        /// Whether the update succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Context scope.
        /// </summary>
        public ContextScopeEnum Scope { get; set; } = ContextScopeEnum.Database;

        /// <summary>
        /// Table metadata identifier for table-scoped context.
        /// </summary>
        public string TableId { get; set; } = null;

        /// <summary>
        /// Table name for table-scoped context.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Saved context.
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
        /// Number of update records processed.
        /// </summary>
        public int TotalRecords { get; set; } = 0;

        /// <summary>
        /// Number of successful updates.
        /// </summary>
        public int Succeeded { get; set; } = 0;

        /// <summary>
        /// Number of failed updates.
        /// </summary>
        public int Failed { get; set; } = 0;

        /// <summary>
        /// Per-entity update results.
        /// </summary>
        public List<McpContextUpdateItemResponse> Objects
        {
            get { return _Objects; }
            set { _Objects = value ?? new List<McpContextUpdateItemResponse>(); }
        }

        private List<McpContextUpdateItemResponse> _Objects = new List<McpContextUpdateItemResponse>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpContextUpdateResponse()
        {
        }
    }
}
