namespace Tablix.Core.Models
{
    using System;

    /// <summary>
    /// MCP request for discovering configured databases.
    /// </summary>
    public class McpDiscoverDatabasesRequest
    {
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
        /// Optional database ID or name filter.
        /// </summary>
        public string Filter { get; set; } = null;

        private int _MaxResults = 100;
        private int _Skip = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpDiscoverDatabasesRequest()
        {
        }
    }
}
