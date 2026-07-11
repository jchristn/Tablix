namespace Tablix.Core.Models
{
    /// <summary>
    /// Paginated table-list response for a database.
    /// </summary>
    public class DatabaseTableListResult : EnumerationResult<TableSummary>
    {
        #region Public-Members

        /// <summary>
        /// Database entry identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// User-supplied context describing the database.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Whether the database schema has been crawled successfully.
        /// </summary>
        public bool IsCrawled { get; set; } = false;

        /// <summary>
        /// Total number of tables in the crawled database before filtering.
        /// </summary>
        public int TableCount { get; set; } = 0;

        /// <summary>
        /// Optional filter applied to table or schema names.
        /// </summary>
        public string Filter { get; set; } = null;

        /// <summary>
        /// Optional schema filter applied to the table list.
        /// </summary>
        public string Schema { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseTableListResult()
        {
        }

        #endregion
    }
}
