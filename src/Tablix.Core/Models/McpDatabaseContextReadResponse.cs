namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// MCP response for database-level context reads.
    /// </summary>
    public class McpDatabaseContextReadResponse : EnumerationResult<DatabaseContextRead>
    {
        /// <summary>
        /// Database identifiers that were requested but not found.
        /// </summary>
        public List<string> MissingDatabaseIds
        {
            get { return _MissingDatabaseIds; }
            set { _MissingDatabaseIds = value ?? new List<string>(); }
        }

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        private List<string> _MissingDatabaseIds = new List<string>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpDatabaseContextReadResponse()
        {
        }
    }
}
