namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Metadata for a database index.
    /// </summary>
    public class IndexDetail
    {
        #region Public-Members

        /// <summary>
        /// Index name.
        /// </summary>
        public string IndexName { get; set; } = null;

        /// <summary>
        /// Columns included in the index.
        /// </summary>
        public List<string> Columns
        {
            get { return _Columns; }
            set { _Columns = value ?? new List<string>(); }
        }

        /// <summary>
        /// Whether the index enforces uniqueness.
        /// </summary>
        public bool IsUnique { get; set; } = false;

        #endregion

        #region Private-Members

        private List<string> _Columns = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public IndexDetail()
        {
        }

        #endregion
    }
}
