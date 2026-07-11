namespace Tablix.Core.Models
{
    /// <summary>
    /// Compact summary of a discovered database table.
    /// </summary>
    public class TableSummary
    {
        #region Public-Members

        /// <summary>
        /// Persisted table metadata identifier.
        /// </summary>
        public string TableId { get; set; } = null;

        /// <summary>
        /// Schema name.
        /// </summary>
        public string SchemaName { get; set; } = null;

        /// <summary>
        /// Table name.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Number of discovered columns in the table.
        /// </summary>
        public int Columns { get; set; } = 0;

        /// <summary>
        /// Number of declared foreign keys from this table.
        /// </summary>
        public int ForeignKeys { get; set; } = 0;

        /// <summary>
        /// Number of discovered indexes on the table.
        /// </summary>
        public int Indexes { get; set; } = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TableSummary()
        {
        }

        #endregion
    }
}
