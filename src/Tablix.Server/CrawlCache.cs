namespace Tablix.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// In-memory cache for database crawl results.
    /// </summary>
    public class CrawlCache
    {
        #region Private-Members

        private readonly ConcurrentDictionary<string, DatabaseDetail> _Cache = new ConcurrentDictionary<string, DatabaseDetail>(StringComparer.OrdinalIgnoreCase);
        private readonly Action<string> _LogInfo;
        private readonly Action<string> _LogWarn;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="logInfo">Info logging delegate.</param>
        /// <param name="logWarn">Warning logging delegate.</param>
        public CrawlCache(Action<string> logInfo = null, Action<string> logWarn = null)
        {
            _LogInfo = logInfo;
            _LogWarn = logWarn;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Crawl all configured databases. Failures are non-fatal.
        /// </summary>
        /// <param name="databases">Database entries to crawl.</param>
        public async Task CrawlAllAsync(List<DatabaseEntry> databases)
        {
            if (databases == null) return;

            foreach (DatabaseEntry entry in databases)
            {
                await CrawlOneAsync(entry).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Crawl a single database. Non-fatal on failure.
        /// </summary>
        /// <param name="entry">Database entry to crawl.</param>
        /// <returns>Database detail result.</returns>
        public async Task<DatabaseDetail> CrawlOneAsync(DatabaseEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            try
            {
                _LogInfo?.Invoke("crawling database '" + entry.Id + "'");
                IDatabaseCrawler crawler = CrawlerFactory.Create(entry.Type);
                DatabaseDetail detail = await crawler.CrawlAsync(entry).ConfigureAwait(false);
                _Cache[entry.Id] = detail;
                _LogInfo?.Invoke("crawled database '" + entry.Id + "': " + detail.Tables.Count + " tables");
                return detail;
            }
            catch (Exception ex)
            {
                DatabaseDetail degraded = new DatabaseDetail
                {
                    DatabaseId = entry.Id,
                    Type = entry.Type,
                    DatabaseName = entry.DatabaseName ?? entry.Filename,
                    Schema = entry.Schema,
                    Context = entry.Context,
                    IsCrawled = false,
                    CrawlError = ex.Message
                };

                _Cache[entry.Id] = degraded;
                _LogWarn?.Invoke("failed to crawl database '" + entry.Id + "': " + ex.Message);
                return degraded;
            }
        }

        /// <summary>
        /// Get cached detail for a database.
        /// </summary>
        /// <param name="id">Database entry ID.</param>
        /// <returns>Cached detail or null.</returns>
        public DatabaseDetail Get(string id)
        {
            if (String.IsNullOrEmpty(id)) return null;
            _Cache.TryGetValue(id, out DatabaseDetail detail);
            return detail;
        }

        /// <summary>
        /// Remove a database from the cache.
        /// </summary>
        /// <param name="id">Database entry ID.</param>
        public void Remove(string id)
        {
            if (!String.IsNullOrEmpty(id))
                _Cache.TryRemove(id, out _);
        }

        /// <summary>
        /// Get all cached details.
        /// </summary>
        /// <returns>List of all cached database details.</returns>
        public List<DatabaseDetail> GetAll()
        {
            return _Cache.Values.ToList();
        }

        #endregion
    }
}
