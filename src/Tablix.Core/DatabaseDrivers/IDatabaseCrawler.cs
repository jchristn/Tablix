namespace Tablix.Core.DatabaseDrivers
{
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// Interface for database schema crawlers and query executors.
    /// </summary>
    public interface IDatabaseCrawler
    {
        /// <summary>
        /// Crawl the database schema and return table geometry.
        /// </summary>
        /// <param name="entry">Database connection configuration.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database detail with discovered tables, columns, foreign keys, and indexes.</returns>
        Task<DatabaseDetail> CrawlAsync(DatabaseEntry entry, CancellationToken token = default);

        /// <summary>
        /// Execute a SQL query against the database.
        /// </summary>
        /// <param name="entry">Database connection configuration.</param>
        /// <param name="query">SQL query to execute.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Query result with data table.</returns>
        Task<QueryResult> ExecuteQueryAsync(DatabaseEntry entry, string query, CancellationToken token = default);

        /// <summary>
        /// Test connectivity to the database.
        /// </summary>
        /// <param name="entry">Database connection configuration.</param>
        /// <param name="token">Cancellation token.</param>
        Task TestConnectionAsync(DatabaseEntry entry, CancellationToken token = default);
    }
}
