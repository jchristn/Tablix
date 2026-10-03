namespace Tablix.Core.DatabaseDrivers
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Models;
    using Tablix.Core.Observability;
    using Tablix.Core.Settings;

    /// <summary>
    /// Decorator that wraps an <see cref="IDatabaseCrawler"/> with a client span ("{db.system.name} {operation}") and
    /// the <see cref="TelemetryNames.MetricDbClientOperations"/> family for every crawl, query, and connectivity test
    /// against a user-configured database. Behavior, return values, and exceptions of the inner crawler are unchanged.
    /// Query text, credentials, and result rows are never recorded.
    /// Thread safety: as thread-safe as the wrapped crawler.
    /// </summary>
    public class InstrumentedDatabaseCrawler : IDatabaseCrawler
    {
        #region Public-Members

        /// <summary>
        /// The wrapped crawler. Never null.
        /// </summary>
        public IDatabaseCrawler Inner
        {
            get { return _Inner; }
        }

        #endregion

        #region Private-Members

        private readonly IDatabaseCrawler _Inner;
        private readonly string _DbSystem;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="inner">Crawler to wrap.</param>
        /// <param name="dbSystem">OpenTelemetry db.system.name value for the wrapped crawler.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="inner"/> is null.</exception>
        public InstrumentedDatabaseCrawler(IDatabaseCrawler inner, string dbSystem)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _DbSystem = String.IsNullOrWhiteSpace(dbSystem) ? TelemetryNames.ValueUnknown : dbSystem;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<DatabaseDetail> CrawlAsync(DatabaseEntry entry, CancellationToken token = default)
        {
            return CrawlAsync(entry, null, token);
        }

        /// <inheritdoc />
        public async Task<DatabaseDetail> CrawlAsync(DatabaseEntry entry, Func<CrawlProgressUpdate, Task> progressCallback, CancellationToken token = default)
        {
            long start = Stopwatch.GetTimestamp();
            Activity activity = StartClientSpan(TelemetryNames.DbOperationCrawl, entry);
            TablixMetrics.AddDbClientActive(_DbSystem, 1);
            string outcome = TelemetryNames.OutcomeSuccess;
            try
            {
                DatabaseDetail detail = await _Inner.CrawlAsync(entry, progressCallback, token).ConfigureAwait(false);
                TablixTracing.SetTag(activity, TelemetryNames.AttrTableCount, detail?.Tables?.Count ?? 0);
                TablixTracing.SetSuccess(activity);
                return detail;
            }
            catch (Exception ex)
            {
                outcome = TablixMetrics.OutcomeOf(ex);
                TablixTracing.RecordException(activity, ex);
                throw;
            }
            finally
            {
                Complete(activity, TelemetryNames.DbOperationCrawl, outcome, start);
            }
        }

        /// <inheritdoc />
        public async Task<QueryResult> ExecuteQueryAsync(DatabaseEntry entry, string query, CancellationToken token = default)
        {
            long start = Stopwatch.GetTimestamp();
            Activity activity = StartClientSpan(TelemetryNames.DbOperationQuery, entry);
            TablixTracing.SetTag(activity, TelemetryNames.AttrStatement, QueryValidatorStatement(query));
            TablixMetrics.AddDbClientActive(_DbSystem, 1);
            string outcome = TelemetryNames.OutcomeSuccess;
            try
            {
                QueryResult result = await _Inner.ExecuteQueryAsync(entry, query, token).ConfigureAwait(false);
                if (result != null && result.Success)
                {
                    TablixTracing.SetTag(activity, TelemetryNames.AttrRowCount, result.RowsReturned);
                    TablixTracing.SetSuccess(activity);
                }
                else
                {
                    outcome = TelemetryNames.OutcomeFailure;
                    TablixTracing.SetOutcome(activity, outcome, "query_failed", "The database reported a query failure.");
                }

                return result;
            }
            catch (Exception ex)
            {
                outcome = TablixMetrics.OutcomeOf(ex);
                TablixTracing.RecordException(activity, ex);
                throw;
            }
            finally
            {
                Complete(activity, TelemetryNames.DbOperationQuery, outcome, start);
            }
        }

        /// <inheritdoc />
        public async Task TestConnectionAsync(DatabaseEntry entry, CancellationToken token = default)
        {
            long start = Stopwatch.GetTimestamp();
            Activity activity = StartClientSpan(TelemetryNames.DbOperationTestConnection, entry);
            TablixMetrics.AddDbClientActive(_DbSystem, 1);
            string outcome = TelemetryNames.OutcomeSuccess;
            try
            {
                await _Inner.TestConnectionAsync(entry, token).ConfigureAwait(false);
                TablixTracing.SetSuccess(activity);
            }
            catch (Exception ex)
            {
                outcome = TablixMetrics.OutcomeOf(ex);
                TablixTracing.RecordException(activity, ex);
                throw;
            }
            finally
            {
                Complete(activity, TelemetryNames.DbOperationTestConnection, outcome, start);
            }
        }

        #endregion

        #region Private-Methods

        private Activity StartClientSpan(string operation, DatabaseEntry entry)
        {
            Activity activity = TablixTracing.Start(_DbSystem + " " + operation, ActivityKind.Client);
            if (activity == null) return null;

            TablixTracing.SetTag(activity, TelemetryNames.AttrDbSystem, _DbSystem);
            TablixTracing.SetTag(activity, TelemetryNames.AttrDbOperation, operation);
            if (entry != null)
            {
                TablixTracing.SetTag(activity, TelemetryNames.AttrDatabaseId, entry.Id);
                if (!String.IsNullOrWhiteSpace(entry.DatabaseName)) TablixTracing.SetTag(activity, TelemetryNames.AttrDbNamespace, entry.DatabaseName);
                if (!String.IsNullOrWhiteSpace(entry.Hostname)) TablixTracing.SetTag(activity, TelemetryNames.AttrServerAddress, entry.Hostname);
                if (entry.Port.HasValue && entry.Port.Value > 0) TablixTracing.SetTag(activity, TelemetryNames.AttrServerPort, entry.Port.Value);
            }

            return activity;
        }

        private void Complete(Activity activity, string operation, string outcome, long start)
        {
            TablixMetrics.AddDbClientActive(_DbSystem, -1);
            TablixMetrics.RecordDbClientOperation(_DbSystem, operation, outcome, TablixMetrics.SecondsSince(start));
            try { activity?.Dispose(); } catch (Exception) { }
        }

        private static string QueryValidatorStatement(string query)
        {
            return Helpers.QueryValidator.GetStatementType(query);
        }

        #endregion
    }
}
