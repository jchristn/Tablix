namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Paginated enumeration response.
    /// </summary>
    /// <typeparam name="T">Type of enumerated objects.</typeparam>
    public class EnumerationResult<T>
    {
        #region Public-Members

        /// <summary>
        /// Whether the request succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Maximum results requested.
        /// </summary>
        public int MaxResults { get; set; } = 100;

        /// <summary>
        /// Number of records skipped.
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Total number of matching records.
        /// </summary>
        public long TotalRecords { get; set; } = 0;

        /// <summary>
        /// Number of records remaining after this page.
        /// </summary>
        public long RecordsRemaining { get; set; } = 0;

        /// <summary>
        /// Whether this is the last page.
        /// </summary>
        public bool EndOfResults { get; set; } = true;

        /// <summary>
        /// Total time in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        /// <summary>
        /// Result objects.
        /// </summary>
        [JsonPropertyOrder(999)]
        public List<T> Objects
        {
            get { return _Objects; }
            set { _Objects = value ?? new List<T>(); }
        }

        #endregion

        #region Private-Members

        private List<T> _Objects = new List<T>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EnumerationResult()
        {
        }

        #endregion
    }
}
