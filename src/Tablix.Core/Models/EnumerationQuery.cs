namespace Tablix.Core.Models
{
    using System;

    /// <summary>
    /// Pagination and filtering parameters for enumeration requests.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of results to return.
        /// </summary>
        public int MaxResults
        {
            get { return _MaxResults; }
            set { _MaxResults = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Number of records to skip.
        /// </summary>
        public int Skip
        {
            get { return _Skip; }
            set { _Skip = Math.Max(value, 0); }
        }

        /// <summary>
        /// Optional filter string to match against Id or DatabaseName.
        /// </summary>
        public string Filter { get; set; } = null;

        #endregion

        #region Private-Members

        private int _MaxResults = 100;
        private int _Skip = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationQuery()
        {
        }

        #endregion
    }
}
