namespace Tablix.Core.Models
{
    /// <summary>
    /// Server-sent event payload for database crawl progress.
    /// </summary>
    public class CrawlProgressEvent
    {
        #region Public-Members

        /// <summary>
        /// Event type, such as started, progress, completed, or failed.
        /// </summary>
        public string EventType { get; set; } = null;

        /// <summary>
        /// Stable crawl stage identifier.
        /// </summary>
        public string Stage { get; set; } = null;

        /// <summary>
        /// Database entry identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Human-readable progress message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Progress percentage from 0 to 100.
        /// </summary>
        public int Percent { get; set; } = 0;

        /// <summary>
        /// Whether this event is terminal.
        /// </summary>
        public bool Terminal { get; set; } = false;

        /// <summary>
        /// Server-side elapsed time in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        /// <summary>
        /// Number of discovered tables, when known.
        /// </summary>
        public int? TableCount { get; set; } = null;

        /// <summary>
        /// Table currently being examined.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// One-based table index for table-level progress.
        /// </summary>
        public int? TableIndex { get; set; } = null;

        /// <summary>
        /// Number of discovered relationships, when known.
        /// </summary>
        public int? RelationshipCount { get; set; } = null;

        /// <summary>
        /// Error message for failed crawl events.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Final crawl detail on completion or degraded failure.
        /// </summary>
        public DatabaseDetail Detail { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CrawlProgressEvent()
        {
        }

        #endregion
    }
}
