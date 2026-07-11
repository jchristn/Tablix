namespace Tablix.Core.Models
{
    /// <summary>
    /// Paginated relationship-list response for a database.
    /// </summary>
    public class DatabaseRelationshipListResult : EnumerationResult<RelationshipDetail>
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
        /// Total number of tables in the crawled database.
        /// </summary>
        public int TableCount { get; set; } = 0;

        /// <summary>
        /// Optional filter applied to table, column, or constraint names.
        /// </summary>
        public string Filter { get; set; } = null;

        /// <summary>
        /// Optional schema filter applied to source or target schema names.
        /// </summary>
        public string Schema { get; set; } = null;

        /// <summary>
        /// Whether inferred relationships were requested.
        /// </summary>
        public bool IncludeInferred { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseRelationshipListResult()
        {
        }

        #endregion
    }
}
