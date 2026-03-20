namespace Tablix.Core.Models
{
    /// <summary>
    /// Metadata for a foreign key relationship.
    /// </summary>
    public class ForeignKeyDetail
    {
        #region Public-Members

        /// <summary>
        /// Constraint name.
        /// </summary>
        public string ConstraintName { get; set; } = null;

        /// <summary>
        /// Source column name.
        /// </summary>
        public string ColumnName { get; set; } = null;

        /// <summary>
        /// Referenced table name.
        /// </summary>
        public string ReferencedTable { get; set; } = null;

        /// <summary>
        /// Referenced column name.
        /// </summary>
        public string ReferencedColumn { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ForeignKeyDetail()
        {
        }

        #endregion
    }
}
