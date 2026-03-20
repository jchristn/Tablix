namespace Tablix.Core.Models
{
    using SerializableDataTables;

    /// <summary>
    /// Result of a SQL query execution.
    /// </summary>
    public class QueryResult
    {
        #region Public-Members

        /// <summary>
        /// Whether the query executed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Number of rows returned.
        /// </summary>
        public int RowsReturned { get; set; } = 0;

        /// <summary>
        /// Total execution time in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        /// <summary>
        /// Result data as a serializable data table.
        /// </summary>
        public SerializableDataTable Data { get; set; } = null;

        /// <summary>
        /// Error message if the query failed.
        /// </summary>
        public string Error { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public QueryResult()
        {
        }

        #endregion
    }
}
