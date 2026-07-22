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
    using Tablix.Core.Persistence;
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
        private readonly DatabaseDriverBase _Persistence;
        private readonly CrawlCache _CrawlCache;
        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="crawlCache">Crawl cache.</param>
        public DatabaseHandler(SettingsManager settingsManager, DatabaseDriverBase persistence, CrawlCache crawlCache)
        {
            _SettingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
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

            long totalRecords = await _Persistence.DatabaseConnections.CountAsync(filter, req.CancellationToken).ConfigureAwait(false);
            List<DatabaseEntry> page = await _Persistence.DatabaseConnections.EnumerateAsync(maxResults, skip, filter, req.CancellationToken).ConfigureAwait(false);
            List<DatabaseSummary> summaries = new List<DatabaseSummary>();
            foreach (DatabaseEntry database in page)
            {
                DatabaseDetail detail = await GetPersistedOrCachedDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
                summaries.Add(DatabaseSummary.From(database, detail));
            }
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
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = await GetPersistedOrCachedDetailAsync(id, req.CancellationToken).ConfigureAwait(false);
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
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
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
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
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
        /// GET /v1/database/{id}/intelligence - derive domain, relationship, ambiguity, and quality guidance.
        /// </summary>
        public async Task<object> GetIntelligenceAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            bool includeAgentPack = true;
            string includeAgentPackStr = req.Http.Request.Query.Elements.Get("includeAgentPack");
            if (!String.IsNullOrEmpty(includeAgentPackStr) && Boolean.TryParse(includeAgentPackStr, out bool parsedIncludeAgentPack))
                includeAgentPack = parsedIncludeAgentPack;

            DatabaseDetail detail = await GetOrCrawlDetailAsync(entry).ConfigureAwait(false);
            if (detail != null)
                detail.Context = entry.Context;

            return DatabaseIntelligenceBuilder.Build(entry, detail, includeAgentPack);
        }

        /// <summary>
        /// GET /v1/database/{id}/agent-pack - derive MCP-ready agent instructions.
        /// </summary>
        public async Task<object> GetAgentPackAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = await GetOrCrawlDetailAsync(entry).ConfigureAwait(false);
            if (detail != null)
                detail.Context = entry.Context;

            DatabaseIntelligenceResponse intelligence = DatabaseIntelligenceBuilder.Build(entry, detail, true);
            return intelligence.AgentPack;
        }

        /// <summary>
        /// GET /v1/database/{id}/table-context - list table contexts for a database.
        /// </summary>
        public async Task<object> ListTableContextsAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            List<TableContextRead> contexts = await _Persistence.TableContexts.EnumerateAsync(id, req.CancellationToken).ConfigureAwait(false);
            return contexts;
        }

        /// <summary>
        /// GET /v1/database/{id}/table-context/{tableId} - read table context.
        /// </summary>
        public async Task<object> GetTableContextAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            string tableId = req.Parameters["tableId"];
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            TableContextRead context = await _Persistence.TableContexts.ReadAsync(id, tableId, req.CancellationToken).ConfigureAwait(false);
            if (context == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Table context '" + tableId + "' not found.");
            }

            return context;
        }

        /// <summary>
        /// PUT /v1/database/{id}/table-context/{tableId} - update table context.
        /// </summary>
        public async Task<object> UpdateTableContextAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            string tableId = req.Parameters["tableId"];
            TableContextUpdateRequest request = req.GetData<TableContextUpdateRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = await GetPersistedOrCachedDetailAsync(id, req.CancellationToken).ConfigureAwait(false);
            TableDetail table = FindTable(detail, tableId);
            if (table == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Table '" + tableId + "' not found in persisted crawl metadata.");
            }

            try
            {
                TableContextRead updated = await _Persistence.TableContexts.UpsertAsync(
                    id,
                    tableId,
                    request.Context,
                    request.Mode,
                    request.Source,
                    req.CancellationToken).ConfigureAwait(false);

                table.Context = updated.Context;
                return updated;
            }
            catch (ArgumentException ex)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, ex.Message);
            }
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
                DatabaseEntry created = await _Persistence.DatabaseConnections.CreateAsync(entry, req.CancellationToken).ConfigureAwait(false);

                // Trigger an initial crawl so the database is immediately usable
                _ = CrawlAndPersistAsync(created, req.CancellationToken);

                req.Http.Response.StatusCode = 201;
                return DatabaseSummary.From(created);
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
                DatabaseEntry existing = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
                if (existing == null)
                {
                    req.Http.Response.StatusCode = 404;
                    return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
                }

                if (String.IsNullOrEmpty(entry.User))
                    entry.User = existing.User;

                if (String.IsNullOrEmpty(entry.Password))
                    entry.Password = existing.Password;

                if (entry.Context == null)
                    entry.Context = existing.Context;

                DatabaseEntry updated = await _Persistence.DatabaseConnections.UpdateAsync(entry, true, req.CancellationToken).ConfigureAwait(false);

                // Update the cached crawl detail so the dashboard reflects changes immediately
                DatabaseDetail cached = _CrawlCache.Get(id);
                if (cached != null)
                {
                    cached.Context = updated.Context;
                    cached.DatabaseName = updated.DatabaseName ?? updated.Filename;
                    cached.Schema = updated.Schema;
                }

                return DatabaseSummary.From(updated, cached);
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
        public async Task<object> UpdateDatabaseContextAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            ContextUpdateRequest request = req.GetData<ContextUpdateRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
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
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Unsupported context update mode '" + request.Mode + "'.");
            }

            string context = await _Persistence.DatabaseContexts.UpsertAsync(id, request.Context, mode, "user", req.CancellationToken).ConfigureAwait(false);
            entry.Context = context;

            DatabaseDetail cached = _CrawlCache.Get(id);
            if (cached != null)
                cached.Context = context;

            return new { Success = true, DatabaseId = id, Context = context, Mode = mode.ToLowerInvariant() };
        }

        /// <summary>
        /// POST /v1/database/test - test unsaved database settings.
        /// </summary>
        public async Task<object> TestDatabaseRequestAsync(AppRequest req)
        {
            DatabaseConnectivityTestRequest request = req.GetData<DatabaseConnectivityTestRequest>();
            if (request == null || request.Database == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Database settings are required.");
            }

            return await TestDatabaseAsync(request.Database, req.CancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// POST /v1/database/{id}/test - test saved database settings.
        /// </summary>
        public async Task<object> TestSavedDatabaseAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            return await TestDatabaseAsync(entry, req.CancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// DELETE /v1/database/{id} — delete a database entry.
        /// </summary>
        public async Task<object> DeleteDatabaseAsync(AppRequest req)
        {
            string id = req.Parameters["id"];

            try
            {
                bool deleted = await _Persistence.DatabaseConnections.DeleteAsync(id, req.CancellationToken).ConfigureAwait(false);
                if (!deleted)
                    throw new KeyNotFoundException("Database with ID '" + id + "' not found.");
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
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (entry == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = await _CrawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
            await _Persistence.DatabaseMetadata.SaveCrawlAsync(detail, req.CancellationToken).ConfigureAwait(false);
            return detail;
        }

        /// <summary>
        /// POST /v1/database/{id}/crawl/stream - re-crawl the database schema and stream status events.
        /// </summary>
        public async Task<object> CrawlDatabaseStreamAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
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
                await _Persistence.DatabaseMetadata.SaveCrawlAsync(detail, req.CancellationToken).ConfigureAwait(false);
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
            DatabaseEntry entry = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
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
            DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(entry.Id).ConfigureAwait(false);
            if (detail == null)
                detail = _CrawlCache.Get(entry.Id);
            if (detail == null)
            {
                detail = await _CrawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                await _Persistence.DatabaseMetadata.SaveCrawlAsync(detail).ConfigureAwait(false);
            }

            return detail;
        }

        private async Task<DatabaseDetail> GetPersistedOrCachedDetailAsync(string databaseId, CancellationToken token)
        {
            DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(databaseId, token).ConfigureAwait(false);
            if (detail != null) return detail;
            return _CrawlCache.Get(databaseId);
        }

        private async Task CrawlAndPersistAsync(DatabaseEntry entry, CancellationToken token)
        {
            DatabaseDetail detail = await _CrawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
            await _Persistence.DatabaseMetadata.SaveCrawlAsync(detail, token).ConfigureAwait(false);
        }

        private static TableDetail FindTable(DatabaseDetail detail, string tableId)
        {
            if (detail == null || detail.Tables == null || String.IsNullOrWhiteSpace(tableId)) return null;

            foreach (TableDetail table in detail.Tables)
            {
                if (String.Equals(table.TableId, tableId, StringComparison.OrdinalIgnoreCase))
                    return table;
            }

            return null;
        }

        private static async Task<DatabaseConnectivityTestResponse> TestDatabaseAsync(DatabaseEntry entry, System.Threading.CancellationToken token)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            DatabaseConnectivityTestResponse result = new DatabaseConnectivityTestResponse
            {
                DatabaseId = entry.Id
            };

            try
            {
                IDatabaseCrawler crawler = CrawlerFactory.Create(entry.Type);
                await crawler.TestConnectionAsync(entry, token).ConfigureAwait(false);
                stopwatch.Stop();
                result.Success = true;
                result.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Message = "Database connection succeeded.";
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                result.Success = false;
                result.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                result.Error = SanitizeDatabaseError(ex.Message, entry);
                return result;
            }
        }

        private static string SanitizeDatabaseError(string message, DatabaseEntry entry)
        {
            string sanitized = message ?? String.Empty;
            if (entry != null)
            {
                if (!String.IsNullOrEmpty(entry.User))
                    sanitized = sanitized.Replace(entry.User, "[redacted]", StringComparison.Ordinal);
                if (!String.IsNullOrEmpty(entry.Password))
                    sanitized = sanitized.Replace(entry.Password, "[redacted]", StringComparison.Ordinal);
            }

            return sanitized;
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
