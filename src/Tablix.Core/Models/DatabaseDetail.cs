namespace Tablix.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Tablix.Core.Enums;

    /// <summary>
    /// Aggregated crawl result for a database, including table geometry and user context.
    /// </summary>
    public class DatabaseDetail
    {
        #region Public-Members

        /// <summary>
        /// Database entry identifier.
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
        /// User-supplied context describing the database.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Discovered tables.
        /// </summary>
        public List<TableDetail> Tables
        {
            get { return _Tables; }
            set { _Tables = value ?? new List<TableDetail>(); }
        }

        /// <summary>
        /// Timestamp of the last successful crawl in UTC.
        /// </summary>
        public DateTime? CrawledUtc { get; set; } = null;

        /// <summary>
        /// Whether the crawl has completed successfully.
        /// When false, the database is considered degraded.
        /// </summary>
        public bool IsCrawled { get; set; } = false;

        /// <summary>
        /// Error message from the last failed crawl attempt, if any.
        /// </summary>
        public string CrawlError { get; set; } = null;

        #endregion

        #region Private-Members

        private List<TableDetail> _Tables = new List<TableDetail>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseDetail()
        {
        }

        #endregion
    }
}
