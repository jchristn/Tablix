namespace Tablix.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Tablix.Core.Enums;

    /// <summary>
    /// MCP response for a paged full-database schema discovery request.
    /// </summary>
    public class McpDatabaseDetailPage
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Database type.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Database name.
        /// </summary>
        public string DatabaseName { get; set; } = null;

        /// <summary>
        /// Schema name.
        /// </summary>
        public string Schema { get; set; } = null;

        /// <summary>
        /// Saved context.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Crawl timestamp.
        /// </summary>
        public DateTime? CrawledUtc { get; set; } = null;

        /// <summary>
        /// Whether the database is crawled.
        /// </summary>
        public bool IsCrawled { get; set; } = false;

        /// <summary>
        /// Crawl error.
        /// </summary>
        public string CrawlError { get; set; } = null;

        /// <summary>
        /// Maximum returned records.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Skipped records.
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Total records.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Records remaining.
        /// </summary>
        public long RecordsRemaining { get; set; } = 0;

        /// <summary>
        /// Whether no more records remain.
        /// </summary>
        public bool EndOfResults { get; set; } = true;

        /// <summary>
        /// Next skip value.
        /// </summary>
        public int? NextSkip { get; set; } = null;

        /// <summary>
        /// Table geometry.
        /// </summary>
        public List<TableDetail> Tables
        {
            get { return _Tables; }
            set { _Tables = value ?? new List<TableDetail>(); }
        }

        private List<TableDetail> _Tables = new List<TableDetail>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpDatabaseDetailPage()
        {
        }
    }
}
