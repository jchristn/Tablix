namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// MCP response for table-level context reads.
    /// </summary>
    public class McpTableContextReadResponse : EnumerationResult<TableContextRead>
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Table identifiers that were requested but not found.
        /// </summary>
        public List<string> MissingTableIds
        {
            get { return _MissingTableIds; }
            set { _MissingTableIds = value ?? new List<string>(); }
        }

        /// <summary>
        /// Table names that were requested but not found.
        /// </summary>
        public List<string> MissingTableNames
        {
            get { return _MissingTableNames; }
            set { _MissingTableNames = value ?? new List<string>(); }
        }

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        private List<string> _MissingTableIds = new List<string>();
        private List<string> _MissingTableNames = new List<string>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpTableContextReadResponse()
        {
        }
    }
}
