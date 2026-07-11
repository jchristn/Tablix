namespace Tablix.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// MCP request for reading table-level context.
    /// </summary>
    public class McpGetTableContextRequest
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Single table metadata identifier.
        /// </summary>
        public string TableId { get; set; } = null;

        /// <summary>
        /// Multiple table metadata identifiers.
        /// </summary>
        public List<string> TableIds
        {
            get { return _TableIds; }
            set { _TableIds = value ?? new List<string>(); }
        }

        /// <summary>
        /// Single table name.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Multiple table names.
        /// </summary>
        public List<string> TableNames
        {
            get { return _TableNames; }
            set { _TableNames = value ?? new List<string>(); }
        }

        /// <summary>
        /// Include crawled tables even when no table context has been written.
        /// </summary>
        public bool IncludeEmpty { get; set; } = false;

        /// <summary>
        /// Maximum records to return when listing contexts.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set { _MaxResults = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Records to skip when listing contexts.
        /// </summary>
        public int Skip
        {
            get { return _Skip; }
            set { _Skip = Math.Max(value, 0); }
        }

        /// <summary>
        /// Optional case-insensitive table or schema filter.
        /// </summary>
        public string Filter { get; set; } = null;

        /// <summary>
        /// Optional exact schema filter.
        /// </summary>
        public string Schema { get; set; } = null;

        private List<string> _TableIds = new List<string>();
        private List<string> _TableNames = new List<string>();
        private int _MaxResults = 100;
        private int _Skip = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpGetTableContextRequest()
        {
        }
    }
}
