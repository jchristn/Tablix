namespace Tablix.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Linq;
    using System.Threading.Tasks;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Observability;
    using Tablix.Core.Settings;
    using Tablix.Server.Observability;

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
            await CrawlAllAsync(databases, TelemetryNames.TriggerOnDemand).ConfigureAwait(false);
        }

        /// <summary>
        /// Crawl all configured databases sequentially. Failures are non-fatal. Each job records the time it spent
        /// queued behind earlier jobs in the batch as its "queued" stage.
        /// </summary>
        /// <param name="databases">Database entries to crawl.</param>
        /// <param name="trigger">Crawl trigger label (see <see cref="TelemetryNames"/>), for example startup.</param>
        /// <param name="token">Cancellation token; checked between databases and passed to each crawl.</param>
        public async Task CrawlAllAsync(List<DatabaseEntry> databases, string trigger, CancellationToken token = default)
        {
            if (databases == null) return;

            DateTimeOffset batchStart = DateTimeOffset.UtcNow;
            foreach (DatabaseEntry entry in databases)
            {
                token.ThrowIfCancellationRequested();
                await CrawlOneAsync(entry, null, trigger, batchStart, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Crawl a single database. Non-fatal on failure.
        /// </summary>
        /// <param name="entry">Database entry to crawl.</param>
        /// <returns>Database detail result.</returns>
        public async Task<DatabaseDetail> CrawlOneAsync(DatabaseEntry entry)
        {
            return await CrawlOneAsync(entry, null, TelemetryNames.TriggerOnDemand).ConfigureAwait(false);
        }

        /// <summary>
        /// Crawl a single database. Non-fatal on failure.
        /// </summary>
        /// <param name="entry">Database entry to crawl.</param>
        /// <param name="trigger">Crawl trigger label (see <see cref="TelemetryNames"/>).</param>
        /// <param name="token">Cancellation token passed to the crawler.</param>
        /// <returns>Database detail result.</returns>
        public async Task<DatabaseDetail> CrawlOneAsync(DatabaseEntry entry, string trigger, CancellationToken token = default)
        {
            return await CrawlOneAsync(entry, null, trigger, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Crawl a single database with progress updates. Non-fatal on failure.
        /// </summary>
        /// <param name="entry">Database entry to crawl.</param>
        /// <param name="progressCallback">Optional progress callback.</param>
        /// <returns>Database detail result.</returns>
        public async Task<DatabaseDetail> CrawlOneAsync(DatabaseEntry entry, Func<CrawlProgressUpdate, Task> progressCallback)
        {
            return await CrawlOneAsync(entry, progressCallback, TelemetryNames.TriggerOnDemand).ConfigureAwait(false);
        }

        /// <summary>
        /// Crawl a single database with progress updates. Non-fatal on failure: a failed crawl caches and returns a
        /// degraded detail carrying the error. Emits a "crawl.job" span with a child span per stage
        /// (discover, examine, cache) and the crawl pipeline metrics.
        /// </summary>
        /// <param name="entry">Database entry to crawl.</param>
        /// <param name="progressCallback">Optional progress callback.</param>
        /// <param name="trigger">Crawl trigger label (see <see cref="TelemetryNames"/>). Null means on_demand.</param>
        /// <param name="token">Cancellation token passed to the crawler. A canceled crawl is cached as degraded.</param>
        /// <returns>Database detail result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is null.</exception>
        public async Task<DatabaseDetail> CrawlOneAsync(DatabaseEntry entry, Func<CrawlProgressUpdate, Task> progressCallback, string trigger, CancellationToken token = default)
        {
            return await CrawlOneAsync(entry, progressCallback, trigger, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get cached detail for a database.
        /// </summary>
        /// <param name="id">Database entry ID.</param>
        /// <returns>Cached detail or null.</returns>
        public DatabaseDetail Get(string id)
        {
            if (String.IsNullOrEmpty(id)) return null;
            bool hit = _Cache.TryGetValue(id, out DatabaseDetail detail);
            TablixMetrics.RecordCrawlCacheLookup(hit);
            return detail;
        }

        /// <summary>
        /// Count cached entries by state, for the crawl cache gauge.
        /// </summary>
        /// <returns>Crawled and degraded entry counts.</returns>
        public CrawlCacheCounts GetCounts()
        {
            CrawlCacheCounts counts = new CrawlCacheCounts();
            foreach (DatabaseDetail detail in _Cache.Values)
            {
                if (detail != null && detail.IsCrawled) counts.Crawled++;
                else counts.Degraded++;
            }

            return counts;
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

        #region Private-Methods

        private async Task<DatabaseDetail> CrawlOneAsync(DatabaseEntry entry, Func<CrawlProgressUpdate, Task> progressCallback, string trigger, DateTimeOffset? queuedSince, CancellationToken token)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string triggerLabel = String.IsNullOrWhiteSpace(trigger) ? TelemetryNames.TriggerOnDemand : trigger;
            string dbSystem = TablixMetrics.DbSystem(entry.Type);
            long start = Stopwatch.GetTimestamp();
            string outcome = TelemetryNames.OutcomeSuccess;
            int tableCount = 0;

            using Activity job = TablixTracing.Start(TelemetryNames.SpanCrawlJob);
            TablixTracing.SetTag(job, TelemetryNames.AttrDatabaseId, entry.Id);
            TablixTracing.SetTag(job, TelemetryNames.AttrDbSystem, dbSystem);
            TablixTracing.SetTag(job, TelemetryNames.AttrTrigger, triggerLabel);
            TablixMetrics.AddCrawlActive(1);

            CrawlStageTracker stages = new CrawlStageTracker(job, dbSystem);
            if (queuedSince.HasValue) stages.RecordQueued(queuedSince.Value);

            try
            {
                _LogInfo?.Invoke("crawling database '" + entry.Id + "'");
                stages.Begin(TelemetryNames.StageDiscover);
                IDatabaseCrawler crawler = CrawlerFactory.Create(entry.Type);
                DatabaseDetail detail = await crawler.CrawlAsync(entry, stages.Wrap(progressCallback), token).ConfigureAwait(false);

                stages.Begin(TelemetryNames.StageCache);
                TableIdentity.Assign(detail);
                _Cache[entry.Id] = detail;
                stages.Complete(TelemetryNames.OutcomeSuccess, null);

                tableCount = detail.Tables.Count;
                TablixTracing.SetTag(job, TelemetryNames.AttrTableCount, tableCount);
                TablixTracing.SetSuccess(job);
                _LogInfo?.Invoke("crawled database '" + entry.Id + "': " + detail.Tables.Count + " tables");
                return detail;
            }
            catch (Exception ex)
            {
                outcome = TablixMetrics.OutcomeOf(ex);
                stages.Complete(outcome, ex);
                TablixTracing.RecordException(job, ex);
                TablixMetrics.RecordError(TelemetryNames.ComponentCrawl, ex);

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
            finally
            {
                TablixMetrics.AddCrawlActive(-1);
                TablixMetrics.RecordCrawlJob(triggerLabel, dbSystem, outcome, TablixMetrics.SecondsSince(start), tableCount);
            }
        }

        #endregion
    }
}
