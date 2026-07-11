namespace Tablix.Core.Models
{
    using System;

    /// <summary>
    /// MCP request for discovering one database.
    /// </summary>
    public class McpDiscoverDatabaseRequest
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Optional maximum table geometry objects to return.
        /// </summary>
        public int? MaxTables
        {
            get { return _MaxTables; }
            set { _MaxTables = value.HasValue ? Math.Clamp(value.Value, 1, 1000) : (int?)null; }
        }

        /// <summary>
        /// Tables to skip.
        /// </summary>
        public int Skip
        {
            get { return _Skip; }
            set { _Skip = Math.Max(value, 0); }
        }

        private int? _MaxTables = null;
        private int _Skip = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpDiscoverDatabaseRequest()
        {
        }
    }
}
