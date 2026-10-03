namespace Tablix.Core.Observability
{
    /// <summary>
    /// Snapshot of crawl cache entry counts, reported by the <see cref="TelemetryNames.MetricCrawlCacheEntries"/> gauge.
    /// </summary>
    public class CrawlCacheCounts
    {
        #region Public-Members

        /// <summary>
        /// Entries whose last crawl succeeded. Default 0.
        /// </summary>
        public long Crawled { get; set; } = 0;

        /// <summary>
        /// Entries whose last crawl failed (degraded placeholders). Default 0.
        /// </summary>
        public long Degraded { get; set; } = 0;

        #endregion
    }
}
