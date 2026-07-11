namespace Tablix.Core.Persistence.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Models;

    /// <summary>
    /// Crawled database metadata persistence methods.
    /// </summary>
    public interface IDatabaseMetadataMethods
    {
        /// <summary>
        /// Save a crawl result.
        /// </summary>
        /// <param name="detail">Crawl detail.</param>
        /// <param name="token">Cancellation token.</param>
        Task SaveCrawlAsync(DatabaseDetail detail, CancellationToken token = default);

        /// <summary>
        /// Read the latest persisted database detail.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database detail or null.</returns>
        Task<DatabaseDetail> ReadDetailAsync(string databaseId, CancellationToken token = default);
    }
}
