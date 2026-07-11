namespace Tablix.Server.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
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
            List<DatabaseSummary> summaries = page
                .Select(d => DatabaseSummary.From(d, _CrawlCache.Get(d.Id)))
                .ToList();
            long remaining = Math.Max(0, totalRecords - skip - page.Count);

            stopwatch.Stop();

            EnumerationResult<DatabaseSummary> result = new EnumerationResult<DatabaseSummary>
            {
                Success = true,
                MaxResults = maxResults,
                Skip = skip,
                TotalRecords = totalRecords,
                RecordsRemaining = remaining,
                EndOfResults = remaining == 0,
                NextSkip = remaining == 0 ? null : (int?)(skip + page.Count),
                TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                Objects = summaries
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

            // Merge non-secret settings fields for dashboard display/edit flows.
            return new DatabaseReadDetail
            {
                DatabaseId = detail.DatabaseId,
                Type = detail.Type,
                DatabaseName = detail.DatabaseName,
                Schema = detail.Schema,
                Context = entry.Context,
                Tables = detail.Tables,
                CrawledUtc = detail.CrawledUtc,
                IsCrawled = detail.IsCrawled,
                CrawlError = detail.CrawlError,
                Name = entry.Name,
                Hostname = entry.Hostname,
                Port = entry.Port,
                HasUser = !String.IsNullOrEmpty(entry.User),
                HasPassword = !String.IsNullOrEmpty(entry.Password),
                Filename = entry.Filename,
                AllowedQueries = new List<string>(entry.AllowedQueries)
            };
        }

        /// <summary>
        /// GET /v1/database/{id}/tables - list crawled tables, paginated.
        /// </summary>
        public async Task<object> ListTablesAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = _SettingsManager.GetDatabase(id);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = await GetOrCrawlDetailAsync(entry).ConfigureAwait(false);
            ReadEnumerationQuery(req, out int maxResults, out int skip, out string filter, out string schema);

            return SchemaProjection.CreateTableListResult(
                id,
                detail,
                maxResults,
                skip,
                filter,
                schema);
        }

        /// <summary>
        /// GET /v1/database/{id}/relationships - list compact relationship edges, paginated.
        /// </summary>
        public async Task<object> ListRelationshipsAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = _SettingsManager.GetDatabase(id);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = await GetOrCrawlDetailAsync(entry).ConfigureAwait(false);
            ReadEnumerationQuery(req, out int maxResults, out int skip, out string filter, out string schema);

            bool includeInferred = false;
            string includeInferredStr = req.Http.Request.Query.Elements.Get("includeInferred");
            if (!String.IsNullOrEmpty(includeInferredStr) && Boolean.TryParse(includeInferredStr, out bool parsedIncludeInferred))
                includeInferred = parsedIncludeInferred;

            return SchemaProjection.CreateRelationshipListResult(
                id,
                detail,
                maxResults,
                skip,
                filter,
                schema,
                includeInferred);
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

                // Trigger an initial crawl so the database is immediately usable
                _ = _CrawlCache.CrawlOneAsync(entry);

                req.Http.Response.StatusCode = 201;
                return DatabaseSummary.From(entry);
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
                DatabaseEntry existing = _SettingsManager.GetDatabase(id);
                if (existing == null)
                {
                    req.Http.Response.StatusCode = 404;
                    return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
                }

                if (String.IsNullOrEmpty(entry.User))
                    entry.User = existing.User;

                if (String.IsNullOrEmpty(entry.Password))
                    entry.Password = existing.Password;

                _SettingsManager.UpdateDatabase(entry);

                // Update the cached crawl detail so the dashboard reflects changes immediately
                DatabaseDetail cached = _CrawlCache.Get(id);
                if (cached != null)
                {
                    cached.Context = entry.Context;
                    cached.DatabaseName = entry.DatabaseName ?? entry.Filename;
                    cached.Schema = entry.Schema;
                }

                return DatabaseSummary.From(entry, cached);
            }
            catch (KeyNotFoundException ex)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, ex.Message);
            }
        }

        /// <summary>
        /// POST /v1/database/{id}/context - update only the user-supplied database context.
        /// </summary>
        public Task<object> UpdateDatabaseContextAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            ContextUpdateRequest request = req.GetData<ContextUpdateRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return Task.FromResult((object)new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required."));
            }

            DatabaseEntry entry = _SettingsManager.GetDatabase(id);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return Task.FromResult((object)new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found."));
            }

            string mode = String.IsNullOrWhiteSpace(request.Mode) ? "replace" : request.Mode.Trim();
            if (String.Equals(mode, "append", StringComparison.OrdinalIgnoreCase))
            {
                if (String.IsNullOrEmpty(entry.Context))
                    entry.Context = request.Context;
                else if (!String.IsNullOrEmpty(request.Context))
                    entry.Context = entry.Context.TrimEnd() + Environment.NewLine + Environment.NewLine + request.Context;
            }
            else if (String.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase))
            {
                entry.Context = request.Context;
            }
            else
            {
                req.Http.Response.StatusCode = 400;
                return Task.FromResult((object)new ApiErrorResponse(ApiErrorEnum.BadRequest, "Unsupported context update mode '" + request.Mode + "'."));
            }

            _SettingsManager.UpdateDatabase(entry);

            DatabaseDetail cached = _CrawlCache.Get(id);
            if (cached != null)
                cached.Context = entry.Context;

            return Task.FromResult((object)new { Success = true, DatabaseId = id, Context = entry.Context, Mode = mode.ToLowerInvariant() });
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
        /// POST /v1/database/{id}/crawl/stream - re-crawl the database schema and stream status events.
        /// </summary>
        public async Task<object> CrawlDatabaseStreamAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = _SettingsManager.GetDatabase(id);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            req.Http.Response.StatusCode = 200;
            req.Http.Response.ContentType = "text/event-stream";
            req.Http.Response.ChunkedTransfer = true;
            req.Http.Response.Headers.Add("Cache-Control", "no-cache");
            req.Http.Response.Headers.Add("X-Accel-Buffering", "no");

            await SendCrawlEventAsync(req, new CrawlProgressEvent
            {
                EventType = "started",
                Stage = "queued",
                DatabaseId = id,
                Message = "Crawl request accepted.",
                Percent = 0
            }, false).ConfigureAwait(false);

            await SendCrawlEventAsync(req, new CrawlProgressEvent
            {
                EventType = "progress",
                Stage = "loading_configuration",
                DatabaseId = id,
                Message = "Loaded database configuration.",
                Percent = 8,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds
            }, false).ConfigureAwait(false);

            await SendCrawlEventAsync(req, new CrawlProgressEvent
            {
                EventType = "progress",
                Stage = "discovering_schema",
                DatabaseId = id,
                Message = "Discovering tables, columns, keys, and indexes.",
                Percent = 15,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds
            }, false).ConfigureAwait(false);

            try
            {
                DatabaseDetail detail = await _CrawlCache.CrawlOneAsync(entry, async (update) =>
                {
                    await SendCrawlEventAsync(req, CreateCrawlProgressEvent(id, update, stopwatch), false).ConfigureAwait(false);
                }).ConfigureAwait(false);
                stopwatch.Stop();

                await SendCrawlEventAsync(req, new CrawlProgressEvent
                {
                    EventType = detail.IsCrawled ? "completed" : "failed",
                    Stage = detail.IsCrawled ? "completed" : "degraded",
                    DatabaseId = id,
                    Message = detail.IsCrawled ? "Crawl completed." : "Crawl completed in degraded state.",
                    Percent = 100,
                    Terminal = true,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                    TableCount = detail.Tables.Count,
                    RelationshipCount = CountRelationships(detail),
                    Error = detail.CrawlError,
                    Detail = detail
                }, true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await SendCrawlEventAsync(req, new CrawlProgressEvent
                {
                    EventType = "failed",
                    Stage = "failed",
                    DatabaseId = id,
                    Message = "Crawl failed.",
                    Percent = 100,
                    Terminal = true,
                    TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                    Error = ex.Message
                }, true).ConfigureAwait(false);
            }

            return null;
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

        private async Task<DatabaseDetail> GetOrCrawlDetailAsync(DatabaseEntry entry)
        {
            DatabaseDetail detail = _CrawlCache.Get(entry.Id);
            if (detail == null)
                detail = await _CrawlCache.CrawlOneAsync(entry).ConfigureAwait(false);

            return detail;
        }

        private static void ReadEnumerationQuery(
            AppRequest req,
            out int maxResults,
            out int skip,
            out string filter,
            out string schema)
        {
            maxResults = 100;
            skip = 0;

            string maxResultsStr = req.Http.Request.Query.Elements.Get("maxResults");
            if (!String.IsNullOrEmpty(maxResultsStr) && Int32.TryParse(maxResultsStr, out int parsedMax))
                maxResults = Math.Clamp(parsedMax, 1, 1000);

            string skipStr = req.Http.Request.Query.Elements.Get("skip");
            if (!String.IsNullOrEmpty(skipStr) && Int32.TryParse(skipStr, out int parsedSkip))
                skip = Math.Max(parsedSkip, 0);

            filter = req.Http.Request.Query.Elements.Get("filter");
            schema = req.Http.Request.Query.Elements.Get("schema");
        }

        private static int CountRelationships(DatabaseDetail detail)
        {
            if (detail == null || detail.Tables == null) return 0;

            int count = 0;
            foreach (TableDetail table in detail.Tables)
            {
                if (table != null && table.ForeignKeys != null)
                    count += table.ForeignKeys.Count;
            }

            return count;
        }

        private static CrawlProgressEvent CreateCrawlProgressEvent(string databaseId, CrawlProgressUpdate update, Stopwatch stopwatch)
        {
            int percent = CalculateCrawlPercent(update);
            return new CrawlProgressEvent
            {
                EventType = "progress",
                Stage = update.Stage,
                DatabaseId = databaseId,
                Message = update.Message,
                Percent = percent,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                TableName = update.TableName,
                TableIndex = update.TableIndex,
                TableCount = update.TableCount,
                RelationshipCount = update.RelationshipCount
            };
        }

        private static int CalculateCrawlPercent(CrawlProgressUpdate update)
        {
            if (update == null) return 15;

            if (String.Equals(update.Stage, "tables_discovered", StringComparison.OrdinalIgnoreCase))
                return 20;

            if (String.Equals(update.Stage, "table_examined", StringComparison.OrdinalIgnoreCase))
            {
                if (update.TableIndex.HasValue && update.TableCount.HasValue && update.TableCount.Value > 0)
                {
                    double ratio = Math.Clamp((double)update.TableIndex.Value / update.TableCount.Value, 0, 1);
                    return Math.Clamp(20 + (int)Math.Round(ratio * 65), 21, 85);
                }

                return 45;
            }

            if (String.Equals(update.Stage, "relationships_analyzed", StringComparison.OrdinalIgnoreCase))
                return 92;

            return 35;
        }

        private static async Task SendCrawlEventAsync(AppRequest req, CrawlProgressEvent evt, bool final)
        {
            string eventName = String.IsNullOrWhiteSpace(evt.EventType) ? "progress" : evt.EventType;
            string json = Serializer.SerializeJson(evt, false);
            string frame = "event: " + eventName + "\n"
                + "data: " + json + "\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(frame);
            await req.Http.Response.SendChunk(bytes, final, req.CancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
