namespace Tablix.Core.Models
{
    /// <summary>
    /// Metadata for a single database column.
    /// </summary>
    public class ColumnDetail
    {
        #region Public-Members

        /// <summary>
        /// Column name.
        /// </summary>
        public string ColumnName { get; set; } = null;

        /// <summary>
        /// Data type.
        /// </summary>
        public string DataType { get; set; } = null;

        /// <summary>
        /// Whether the column allows null values.
        /// </summary>
        public bool IsNullable { get; set; } = true;

        /// <summary>
        /// Whether the column is a primary key.
        /// </summary>
        public bool IsPrimaryKey { get; set; } = false;

        /// <summary>
        /// Default value expression, if any.
        /// </summary>
        public string DefaultValue { get; set; } = null;

        /// <summary>
        /// Maximum length, if applicable.
        /// </summary>
        public int? MaxLength { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ColumnDetail()
        {
        }

        #endregion
    }
}
