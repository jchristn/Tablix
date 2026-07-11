namespace Tablix.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// MCP request for reading database-level context.
    /// </summary>
    public class McpGetDatabaseContextRequest
    {
        /// <summary>
        /// Single database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Multiple database identifiers.
        /// </summary>
        public List<string> DatabaseIds
        {
            get { return _DatabaseIds; }
            set { _DatabaseIds = value ?? new List<string>(); }
        }

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
        /// Optional case-insensitive database ID or name filter.
        /// </summary>
        public string Filter { get; set; } = null;

        private List<string> _DatabaseIds = new List<string>();
        private int _MaxResults = 100;
        private int _Skip = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpGetDatabaseContextRequest()
        {
        }
    }
}
