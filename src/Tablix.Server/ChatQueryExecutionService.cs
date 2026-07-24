namespace Tablix.Server
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// Shared chat query execution path for native tool calls and fallback planning.
    /// </summary>
    public class ChatQueryExecutionService
    {
        #region Private-Members

        private readonly CrawlCache _CrawlCache;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="crawlCache">Crawl cache.</param>
        public ChatQueryExecutionService(CrawlCache crawlCache)
        {
            _CrawlCache = crawlCache ?? throw new ArgumentNullException(nameof(crawlCache));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate and execute one query against the selected database.
        /// </summary>
        /// <param name="database">Selected database.</param>
        /// <param name="query">SQL query.</param>
        /// <param name="retryAfterSchemaRefresh">Retry once after a schema refresh when the failure looks schema-related.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Execution result.</returns>
        public async Task<ChatQueryExecutionResult> ExecuteAsync(DatabaseEntry database, string query, bool retryAfterSchemaRefresh, CancellationToken token)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            ChatQueryExecutionResult result = await ExecuteOnceAsync(database, query, token).ConfigureAwait(false);
            if (result.Success || !retryAfterSchemaRefresh || !IsSchemaRelatedFailure(result.Error))
                return result;

            Stopwatch refreshStopwatch = Stopwatch.StartNew();
            DatabaseDetail detail = await _CrawlCache.CrawlOneAsync(database).ConfigureAwait(false);
            refreshStopwatch.Stop();

            ChatQueryExecutionResult retry = await ExecuteOnceAsync(database, query, token).ConfigureAwait(false);
            retry.SchemaRefreshed = true;
            retry.SchemaRefreshMs = refreshStopwatch.Elapsed.TotalMilliseconds;
            retry.SchemaRefreshTableCount = detail == null || detail.Tables == null ? 0 : detail.Tables.Count;
            retry.InitialError = result.Error;
            return retry;
        }

        #endregion

        #region Private-Methods

        private static async Task<ChatQueryExecutionResult> ExecuteOnceAsync(DatabaseEntry database, string query, CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            string normalizedQuery = QueryValidator.NormalizeSingleStatement(query);
            string validationError = QueryValidator.Validate(normalizedQuery, database.AllowedQueries);
            if (validationError != null)
            {
                stopwatch.Stop();
                return new ChatQueryExecutionResult
                {
                    Success = false,
                    ValidationError = validationError,
                    Error = validationError,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }

            try
            {
                IDatabaseCrawler crawler = CrawlerFactory.Create(database.Type);
                QueryResult queryResult = await crawler.ExecuteQueryAsync(database, normalizedQuery, token).ConfigureAwait(false);
                stopwatch.Stop();

                return new ChatQueryExecutionResult
                {
                    Success = queryResult.Success,
                    QueryResult = queryResult,
                    Error = queryResult.Error,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new ChatQueryExecutionResult
                {
                    Success = false,
                    Error = ex.Message,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds
                };
            }
        }

        private static bool IsSchemaRelatedFailure(string error)
        {
            if (String.IsNullOrWhiteSpace(error)) return false;

            string normalized = error.ToLowerInvariant();
            return normalized.Contains("unknown column") ||
                   normalized.Contains("no such column") ||
                   normalized.Contains("invalid column") ||
                   normalized.Contains("column") && normalized.Contains("not found") ||
                   normalized.Contains("column") && normalized.Contains("does not exist") ||
                   normalized.Contains("type mismatch") ||
                   normalized.Contains("datatype mismatch");
        }

        #endregion
    }

}
