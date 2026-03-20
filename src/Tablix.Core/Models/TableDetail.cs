namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Metadata for a discovered database table.
    /// </summary>
    public class TableDetail
    {
        #region Public-Members

        /// <summary>
        /// Table name.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Schema name.
        /// </summary>
        public string SchemaName { get; set; } = null;

        /// <summary>
        /// Columns in the table.
        /// </summary>
        public List<ColumnDetail> Columns
        {
            get { return _Columns; }
            set { _Columns = value ?? new List<ColumnDetail>(); }
        }

        /// <summary>
        /// Foreign keys on the table.
        /// </summary>
        public List<ForeignKeyDetail> ForeignKeys
        {
            get { return _ForeignKeys; }
            set { _ForeignKeys = value ?? new List<ForeignKeyDetail>(); }
        }

        /// <summary>
        /// Indexes on the table.
        /// </summary>
        public List<IndexDetail> Indexes
        {
            get { return _Indexes; }
            set { _Indexes = value ?? new List<IndexDetail>(); }
        }

        #endregion

        #region Private-Members

        private List<ColumnDetail> _Columns = new List<ColumnDetail>();
        private List<ForeignKeyDetail> _ForeignKeys = new List<ForeignKeyDetail>();
        private List<IndexDetail> _Indexes = new List<IndexDetail>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TableDetail()
        {
        }

        #endregion
    }
}
