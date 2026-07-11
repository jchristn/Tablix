namespace Tablix.Core.Models
{
    using System;

    /// <summary>
    /// MCP request for listing compact relationship edges.
    /// </summary>
    public class McpListRelationshipsRequest
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Maximum records to return.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set { _MaxResults = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Records to skip.
        /// </summary>
        public int Skip
        {
            get { return _Skip; }
            set { _Skip = Math.Max(value, 0); }
        }

        /// <summary>
        /// Optional filter.
        /// </summary>
        public string Filter { get; set; } = null;

        /// <summary>
        /// Optional schema filter.
        /// </summary>
        public string Schema { get; set; } = null;

        /// <summary>
        /// Include inferred relationships when supported.
        /// </summary>
        public bool IncludeInferred { get; set; } = false;

        private int _MaxResults = 100;
        private int _Skip = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpListRelationshipsRequest()
        {
        }
    }
}
