namespace Tablix.Core.Models
{
    /// <summary>
    /// Internal progress update emitted by schema crawlers.
    /// </summary>
    public class CrawlProgressUpdate
    {
        #region Public-Members

        /// <summary>
        /// Stable crawl stage identifier.
        /// </summary>
        public string Stage { get; set; } = null;

        /// <summary>
        /// Human-readable progress message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Table currently being examined.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// One-based table index for table-level progress.
        /// </summary>
        public int? TableIndex { get; set; } = null;

        /// <summary>
        /// Total table count, when known.
        /// </summary>
        public int? TableCount { get; set; } = null;

        /// <summary>
        /// Number of relationships discovered or analyzed so far.
        /// </summary>
        public int? RelationshipCount { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CrawlProgressUpdate()
        {
        }

        #endregion
    }
}
