namespace Tablix.Server.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;
    using SwiftStack.Rest;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;

    /// <summary>
    /// REST handlers for database CRUD, crawl, and query operations.
    /// </summary>
    public class DatabaseHandler
    {
        #region Private-Members

        private readonly SettingsManager _SettingsManager;
        private readonly CrawlCache _CrawlCache;
        private readonly string _Header = "[DatabaseHandler] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="crawlCache">Crawl cache.</param>
        public DatabaseHandler(SettingsManager settingsManager, CrawlCache crawlCache)
        {
            _SettingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _CrawlCache = crawlCache ?? throw new ArgumentNullException(nameof(crawlCache));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// GET /v1/database — list all databases, paginated.
        /// </summary>
        public async Task<object> ListDatabasesAsync(AppRequest req)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            int maxResults = 100;
            int skip = 0;
            string filter = null;

            string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
            if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int parsedMax))
                maxResults = Math.Clamp(parsedMax, 1, 1000);

            string skipStr = req.Http.Request.Query.Elements.Get("skip");
            if (!String.IsNullOrEmpty(skipStr) && Int32.TryParse(skipStr, out int parsedSkip))
                skip = Math.Max(parsedSkip, 0);

            filter = req.Http.Request.Query.Elements.Get("filter");

            TablixSettings settings = _SettingsManager.Settings;
            List<DatabaseEntry> databases = settings.Databases;

            if (!String.IsNullOrEmpty(filter))
            {
                databases = databases.Where(d =>
                    (d.Id != null && d.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                    (d.DatabaseName != null && d.DatabaseName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            long totalRecords = databases.Count;
            List<DatabaseEntry> page = databases.Skip(skip).Take(maxResults).ToList();
            long remaining = Math.Max(0, totalRecords - skip - page.Count);

            stopwatch.Stop();

            EnumerationResult<DatabaseEntry> result = new EnumerationResult<DatabaseEntry>
            {
                Success = true,
                MaxResults = maxResults,
                Skip = skip,
                TotalRecords = totalRecords,
                RecordsRemaining = remaining,
                EndOfResults = remaining == 0,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                Objects = page
            };

            return result;
        }

        /// <summary>
        /// GET /v1/database/{id} — get a single database entry with cached crawl detail.
        /// </summary>
        public async Task<object> GetDatabaseAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = _SettingsManager.GetDatabase(id);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = _CrawlCache.Get(id);
            if (detail == null)
            {
                detail = new DatabaseDetail
                {
                    DatabaseId = entry.Id,
                    Type = entry.Type,
                    DatabaseName = entry.DatabaseName ?? entry.Filename,
                    Schema = entry.Schema,
                    Context = entry.Context,
                    IsCrawled = false,
                    CrawlError = "Not yet crawled."
                };
            }

            return detail;
        }

        /// <summary>
        /// POST /v1/database — add a new database entry.
        /// </summary>
        public async Task<object> AddDatabaseAsync(AppRequest req)
        {
            DatabaseEntry entry = req.GetData<DatabaseEntry>();
            if (entry == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            try
            {
                _SettingsManager.AddDatabase(entry);
                req.Http.Response.StatusCode = 201;
                return entry;
            }
            catch (InvalidOperationException ex)
            {
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, ex.Message);
            }
        }

        /// <summary>
        /// PUT /v1/database/{id} — update an existing database entry.
        /// </summary>
        public async Task<object> UpdateDatabaseAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = req.GetData<DatabaseEntry>();
            if (entry == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            entry.Id = id;

            try
            {
                _SettingsManager.UpdateDatabase(entry);
                return entry;
            }
            catch (KeyNotFoundException ex)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, ex.Message);
            }
        }

        /// <summary>
        /// DELETE /v1/database/{id} — delete a database entry.
        /// </summary>
        public async Task<object> DeleteDatabaseAsync(AppRequest req)
        {
            string id = req.Parameters["id"];

            try
            {
                _SettingsManager.DeleteDatabase(id);
                _CrawlCache.Remove(id);
                req.Http.Response.StatusCode = 204;
                return null;
            }
            catch (KeyNotFoundException ex)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, ex.Message);
            }
        }

        /// <summary>
        /// POST /v1/database/{id}/crawl — re-crawl the database schema.
        /// </summary>
        public async Task<object> CrawlDatabaseAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = _SettingsManager.GetDatabase(id);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = await _CrawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
            return detail;
        }

        /// <summary>
        /// POST /v1/database/{id}/query — execute a SQL query.
        /// </summary>
        public async Task<object> ExecuteQueryAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = _SettingsManager.GetDatabase(id);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            QueryRequest queryRequest = req.GetData<QueryRequest>();
            if (queryRequest == null || String.IsNullOrWhiteSpace(queryRequest.Query))
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Query is required.");
            }

            // Validate query against allowed types
            string validationError = QueryValidator.Validate(queryRequest.Query, entry.AllowedQueries);
            if (validationError != null)
            {
                req.Http.Response.StatusCode = 403;
                return new ApiErrorResponse(ApiErrorEnum.Forbidden, validationError);
            }

            try
            {
                IDatabaseCrawler crawler = CrawlerFactory.Create(entry.Type);
                QueryResult result = await crawler.ExecuteQueryAsync(entry, queryRequest.Query).ConfigureAwait(false);
                return result;
            }
            catch (Exception ex)
            {
                req.Http.Response.StatusCode = 500;
                return new ApiErrorResponse(ApiErrorEnum.InternalError, ex.Message);
            }
        }

        #endregion
    }
}
