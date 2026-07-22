namespace Tablix.Server.Mcp
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
    using Tablix.Core.Persistence;
    using Tablix.Core.Settings;

    /// <summary>
    /// Registers all Tablix MCP tools.
    /// </summary>
    public static class McpToolRegistrar
    {
        #region Public-Methods

        /// <summary>
        /// Register all Tablix MCP tools.
        /// </summary>
        /// <param name="register">Tool registration delegate.</param>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="crawlCache">Crawl cache.</param>
        /// <param name="logDebug">Optional debug logging delegate.</param>
        public static void RegisterAll(
            RegisterToolDelegate register,
            DatabaseDriverBase persistence,
            CrawlCache crawlCache,
            Action<string> logDebug = null)
        {
            if (register == null) throw new ArgumentNullException(nameof(register));
            if (persistence == null) throw new ArgumentNullException(nameof(persistence));
            if (crawlCache == null) throw new ArgumentNullException(nameof(crawlCache));

            RegisterDiscoverDatabases(register, persistence, crawlCache, logDebug);
            RegisterDiscoverDatabase(register, persistence, crawlCache, logDebug);
            RegisterListTables(register, persistence, crawlCache, logDebug);
            RegisterDiscoverTable(register, persistence, crawlCache, logDebug);
            RegisterListRelationships(register, persistence, crawlCache, logDebug);
            RegisterExecuteQuery(register, persistence, crawlCache, logDebug);
            RegisterGetDatabaseContext(register, persistence, crawlCache, logDebug);
            RegisterGetTableContext(register, persistence, crawlCache, logDebug);
            RegisterUpdateContext(register, persistence, crawlCache, logDebug);
            RegisterUpdateDatabaseContext(register, persistence, crawlCache, logDebug);
            RegisterUpdateTableContext(register, persistence, crawlCache, logDebug);
            RegisterDatabaseIntelligence(register, persistence, crawlCache, logDebug);
            RegisterAgentPack(register, persistence, crawlCache, logDebug);
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Placeholder for future MCP authentication.
        /// </summary>
        private static bool AuthorizeMcp(string apiKey)
        {
            // v0.1: MCP is trusted, no auth required.
            return true;
        }

        private static void RegisterDiscoverDatabases(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_discover_databases",
                "Use first in every Tablix workflow. Lists configured databases using redacted metadata, AllowedQueries, crawl state, and user-supplied Context. Credentials are never returned; HasUser and HasPassword only indicate whether credentials are configured. Page through results with maxResults and skip until EndOfResults is true; when EndOfResults is false, call again with skip set to NextSkip. Choose the databaseId from this response and preserve any Context as authoritative user guidance. After selecting a database, prefer tablix_list_tables and tablix_list_relationships before requesting full table geometry.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        maxResults = new { type = "integer", description = "Maximum databases to return (1-1000, default 100). Use a smaller value when many databases may be configured." },
                        skip = new { type = "integer", description = "Number of database records to skip (default 0). Use the previous response's NextSkip for the next page." },
                        filter = new { type = "string", description = "Optional case-insensitive filter for database ID or name." }
                    }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_discover_databases invoked");

                    McpDiscoverDatabasesRequest request = ReadRequest<McpDiscoverDatabasesRequest>(args);

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    long totalRecords = await persistence.DatabaseConnections.CountAsync(request.Filter).ConfigureAwait(false);
                    List<DatabaseEntry> page = await persistence.DatabaseConnections.EnumerateAsync(request.MaxResults, request.Skip, request.Filter).ConfigureAwait(false);
                    List<DatabaseSummary> summaries = new List<DatabaseSummary>();
                    foreach (DatabaseEntry database in page)
                    {
                        DatabaseDetail detail = crawlCache.Get(database.Id);
                        if (detail == null)
                            detail = await persistence.DatabaseMetadata.ReadDetailAsync(database.Id).ConfigureAwait(false);
                        summaries.Add(DatabaseSummary.From(database, detail));
                    }
                    long remaining = Math.Max(0, totalRecords - request.Skip - page.Count);

                    stopwatch.Stop();

                    EnumerationResult<DatabaseSummary> result = new EnumerationResult<DatabaseSummary>
                    {
                        Success = true,
                        MaxResults = request.MaxResults,
                        Skip = request.Skip,
                        TotalRecords = totalRecords,
                        RecordsRemaining = remaining,
                        EndOfResults = remaining == 0,
                        NextSkip = remaining == 0 ? null : (int?)(request.Skip + page.Count),
                        TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                        Objects = summaries
                    };

                    return (object)result;
                });
        }

        private static void RegisterDiscoverDatabase(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_discover_database",
                "Returns database schema geometry and can be very large. Use only for small databases or when the user explicitly asks for full-database geometry. For reliable large-schema work, use tablix_list_tables to page table summaries, tablix_list_relationships to inspect declared foreign keys, then tablix_discover_table for specific tables. If you call this tool, set maxTables and page with skip/NextSkip when the database may have many tables. Do not assume the response is complete unless EndOfResults is true or no maxTables paging was requested.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        maxTables = new { type = "integer", description = "Optional maximum number of full table geometry objects to return (1-1000). Prefer a small page size for large databases." },
                        skip = new { type = "integer", description = "Optional number of tables to skip. Use the previous response's NextSkip for continuation." }
                    },
                    required = new[] { "databaseId" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_discover_database invoked");

                    McpDiscoverDatabaseRequest request = ReadRequest<McpDiscoverDatabaseRequest>(args);

                    if (String.IsNullOrEmpty(request.DatabaseId))
                        return (object)new McpErrorResponse("databaseId is required");

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                        return (object)new McpErrorResponse("Database '" + request.DatabaseId + "' not found");

                    DatabaseDetail detail = crawlCache.Get(request.DatabaseId);
                    if (detail == null)
                        detail = await persistence.DatabaseMetadata.ReadDetailAsync(request.DatabaseId).ConfigureAwait(false);
                    if (detail == null)
                    {
                        // Attempt a crawl
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                        await persistence.DatabaseMetadata.SaveCrawlAsync(detail).ConfigureAwait(false);
                    }

                    if (request.MaxTables.HasValue)
                    {
                        List<TableDetail> orderedTables = detail.Tables
                            .OrderBy(t => t.SchemaName)
                            .ThenBy(t => t.TableName)
                            .ToList();
                        List<TableDetail> page = orderedTables.Skip(request.Skip).Take(request.MaxTables.Value).ToList();
                        long remaining = Math.Max(0, orderedTables.Count - request.Skip - page.Count);

                        return (object)new McpDatabaseDetailPage
                        {
                            DatabaseId = detail.DatabaseId,
                            Type = detail.Type,
                            DatabaseName = detail.DatabaseName,
                            Schema = detail.Schema,
                            Context = detail.Context,
                            CrawledUtc = detail.CrawledUtc,
                            IsCrawled = detail.IsCrawled,
                            CrawlError = detail.CrawlError,
                            MaxResults = request.MaxTables.Value,
                            Skip = request.Skip,
                            TotalRecords = orderedTables.Count,
                            RecordsRemaining = remaining,
                            EndOfResults = remaining == 0,
                            NextSkip = remaining == 0 ? null : (int?)(request.Skip + page.Count),
                            Tables = page
                        };
                    }

                    return (object)detail;
                });
        }

        private static void RegisterListTables(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_list_tables",
                "Use after tablix_discover_databases. This is the preferred table-discovery tool for large databases because it returns compact summaries instead of full geometry. Page until EndOfResults is true; when EndOfResults is false, call again with skip set to NextSkip. Use filter or schema to narrow the search when the user asks about a domain, feature, or table family. Do not write SQL from this summary alone unless the query only needs table names; call tablix_discover_table for the columns, keys, and indexes you need.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        maxResults = new { type = "integer", description = "Maximum table summaries to return (1-1000, default 100)." },
                        skip = new { type = "integer", description = "Number of table summaries to skip (default 0). Use the previous response's NextSkip for continuation." },
                        filter = new { type = "string", description = "Optional case-insensitive filter by table or schema name." },
                        schema = new { type = "string", description = "Optional exact schema filter." }
                    },
                    required = new[] { "databaseId" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_list_tables invoked");

                    McpListTablesRequest request = ReadRequest<McpListTablesRequest>(args);

                    if (String.IsNullOrEmpty(request.DatabaseId))
                        return (object)new McpErrorResponse("databaseId is required");

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                        return (object)new McpErrorResponse("Database '" + request.DatabaseId + "' not found");

                    DatabaseDetail detail = crawlCache.Get(request.DatabaseId);
                    if (detail == null)
                        detail = await persistence.DatabaseMetadata.ReadDetailAsync(request.DatabaseId).ConfigureAwait(false);
                    if (detail == null)
                    {
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                        await persistence.DatabaseMetadata.SaveCrawlAsync(detail).ConfigureAwait(false);
                    }

                    return (object)SchemaProjection.CreateTableListResult(
                        request.DatabaseId,
                        detail,
                        request.MaxResults,
                        request.Skip,
                        request.Filter,
                        request.Schema);
                });
        }

        private static void RegisterDiscoverTable(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_discover_table",
                "Use for high-fidelity table geometry before writing SQL. Returns columns, data types, nullability, defaults, primary keys, declared foreign keys, and indexes for one table. Call this for every table you plan to select from, join, filter on, insert into, update, or delete from. Combine it with tablix_list_relationships for join planning. If a table is not found, re-check exact spelling from tablix_list_tables; tableName matching is case-insensitive but schema-qualified names are not required.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        tableName = new { type = "string", description = "Table name to retrieve geometry for. Use the TableName value returned by tablix_list_tables." }
                    },
                    required = new[] { "databaseId", "tableName" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_discover_table invoked");

                    McpDiscoverTableRequest request = ReadRequest<McpDiscoverTableRequest>(args);

                    if (String.IsNullOrEmpty(request.DatabaseId))
                        return (object)new McpErrorResponse("databaseId is required");

                    if (String.IsNullOrEmpty(request.TableName))
                        return (object)new McpErrorResponse("tableName is required");

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                        return (object)new McpErrorResponse("Database '" + request.DatabaseId + "' not found");

                    DatabaseDetail detail = crawlCache.Get(request.DatabaseId);
                    if (detail == null)
                        detail = await persistence.DatabaseMetadata.ReadDetailAsync(request.DatabaseId).ConfigureAwait(false);
                    if (detail == null)
                    {
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                        await persistence.DatabaseMetadata.SaveCrawlAsync(detail).ConfigureAwait(false);
                    }

                    TableDetail table = detail.Tables.FirstOrDefault(t =>
                        String.Equals(t.TableName, request.TableName, StringComparison.OrdinalIgnoreCase));

                    if (table == null)
                        return (object)new McpErrorResponse("Table '" + request.TableName + "' not found in database '" + request.DatabaseId + "'");

                    return (object)new McpTableDetailResponse
                    {
                        DatabaseId = request.DatabaseId,
                        Context = detail.Context,
                        Table = table
                    };
                });
        }

        private static void RegisterListRelationships(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_list_relationships",
                "Use after or alongside tablix_list_tables when you need joins or database context. Lists compact relationship edges in pages. Currently returns declared foreign keys only; Source is declared_fk and Confidence is 1.0. Page until EndOfResults is true; when EndOfResults is false, call again with skip set to NextSkip. Treat absence of a relationship as absence of a declared FK, not proof that tables are unrelated. For implicit relationships, inspect column names with tablix_discover_table and clearly label any inference in your answer or saved context.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        maxResults = new { type = "integer", description = "Maximum relationship edges to return (1-1000, default 100)." },
                        skip = new { type = "integer", description = "Number of relationship edges to skip (default 0). Use the previous response's NextSkip for continuation." },
                        filter = new { type = "string", description = "Optional case-insensitive filter by table, column, schema, or constraint name." },
                        schema = new { type = "string", description = "Optional schema filter for source or referenced schema." },
                        includeInferred = new { type = "boolean", description = "Reserved for future inferred relationships. Currently ignored; only declared foreign keys are returned." }
                    },
                    required = new[] { "databaseId" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_list_relationships invoked");

                    McpListRelationshipsRequest request = ReadRequest<McpListRelationshipsRequest>(args);

                    if (String.IsNullOrEmpty(request.DatabaseId))
                        return (object)new McpErrorResponse("databaseId is required");

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                        return (object)new McpErrorResponse("Database '" + request.DatabaseId + "' not found");

                    DatabaseDetail detail = crawlCache.Get(request.DatabaseId);
                    if (detail == null)
                        detail = await persistence.DatabaseMetadata.ReadDetailAsync(request.DatabaseId).ConfigureAwait(false);
                    if (detail == null)
                    {
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                        await persistence.DatabaseMetadata.SaveCrawlAsync(detail).ConfigureAwait(false);
                    }

                    return (object)SchemaProjection.CreateRelationshipListResult(
                        request.DatabaseId,
                        detail,
                        request.MaxResults,
                        request.Skip,
                        request.Filter,
                        request.Schema,
                        request.IncludeInferred);
                });
        }

        private static void RegisterExecuteQuery(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_execute_query",
                "Execute one SQL statement against a database after discovering enough schema to be confident. Use this tool to answer user requests for actual data or requested database changes, including phrases like show me, how many, count, list, find, total, average, latest, top, summarize, add, update, or delete. Do not merely provide SQL when the user asks for an answer or action and the statement type is allowed; run the permitted query and return the result. The query must contain no semicolons and its statement type must be allowed by the database's AllowedQueries from tablix_discover_databases. Prefer SELECT for exploration. Never run write statements unless the user explicitly asks for the change and AllowedQueries permits that statement type. Check AllowedQueries first. For row-reading queries, project only needed columns and include sensible limits when exploring row data; aggregate queries such as COUNT do not need a LIMIT. Validate table and column names with tablix_discover_table before querying. If execution fails because of a bad or unknown column, missing column, or column type mismatch, refresh schema by re-discovering the relevant table or database before retrying. If refreshed schema proves saved database context has wrong column names, wrong column types, or stale relationship guidance, update database context with tablix_update_database_context. If refreshed schema proves saved table context is stale for a specific table, update that table context with tablix_update_table_context.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        query = new { type = "string", description = "Single SQL statement to execute. Do not include semicolons or multiple statements." }
                    },
                    required = new[] { "databaseId", "query" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_execute_query invoked");

                    McpExecuteQueryRequest request = ReadRequest<McpExecuteQueryRequest>(args);

                    if (String.IsNullOrEmpty(request.DatabaseId))
                        return (object)new QueryResult { Success = false, Error = "databaseId is required" };

                    if (String.IsNullOrEmpty(request.Query))
                        return (object)new QueryResult { Success = false, Error = "query is required" };

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                        return (object)new QueryResult { Success = false, DatabaseId = request.DatabaseId, Error = "Database '" + request.DatabaseId + "' not found" };

                    // Validate query against allowed types
                    string validationError = QueryValidator.Validate(request.Query, entry.AllowedQueries);
                    if (validationError != null)
                        return (object)new QueryResult { Success = false, DatabaseId = request.DatabaseId, Error = validationError };

                    try
                    {
                        IDatabaseCrawler crawler = CrawlerFactory.Create(entry.Type);
                        QueryResult result = await crawler.ExecuteQueryAsync(entry, request.Query).ConfigureAwait(false);
                        return (object)result;
                    }
                    catch (Exception ex)
                    {
                        return (object)new QueryResult { Success = false, DatabaseId = request.DatabaseId, Error = ex.Message };
                    }
                });
        }

        private static void RegisterGetDatabaseContext(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_get_database_context",
                "Read persisted database-level context for one database, multiple databases, or a paged set of configured databases. Use this when you need durable business/domain guidance before answering a natural-language question, before writing SQL, or before deciding whether saved context is stale. Context is guidance, not proof; verify table and column names with schema tools before querying. Use databaseId for one database, databaseIds for multiple specific databases, or omit both to page through configured database contexts. Credentials are never returned.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Optional single database entry ID." },
                        databaseIds = new { type = "array", items = new { type = "string" }, description = "Optional list of database entry IDs." },
                        maxResults = new { type = "integer", description = "Maximum database contexts to return when listing (1-1000, default 100)." },
                        skip = new { type = "integer", description = "Number of database contexts to skip when listing (default 0)." },
                        filter = new { type = "string", description = "Optional case-insensitive filter by database ID or name when listing." }
                    }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_get_database_context invoked");
                    McpGetDatabaseContextRequest request = ReadRequest<McpGetDatabaseContextRequest>(args);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    McpDatabaseContextReadResponse response = new McpDatabaseContextReadResponse
                    {
                        MaxResults = request.MaxResults,
                        Skip = request.Skip
                    };

                    List<string> requestedIds = BuildRequestedDatabaseIds(request);
                    if (requestedIds.Count > 0)
                    {
                        foreach (string databaseId in requestedIds)
                        {
                            DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(databaseId).ConfigureAwait(false);
                            if (entry == null)
                            {
                                response.MissingDatabaseIds.Add(databaseId);
                            }
                            else
                            {
                                response.Objects.Add(ToDatabaseContextRead(entry));
                            }
                        }

                        response.Success = response.MissingDatabaseIds.Count == 0 || response.Objects.Count > 0;
                        response.TotalRecords = response.Objects.Count;
                        response.RecordsRemaining = 0;
                        response.EndOfResults = true;
                        response.NextSkip = null;
                        if (response.Objects.Count == 0 && response.MissingDatabaseIds.Count > 0)
                        {
                            response.Success = false;
                            response.Error = "No requested databases were found.";
                        }

                        stopwatch.Stop();
                        response.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                        return (object)response;
                    }

                    long totalRecords = await persistence.DatabaseConnections.CountAsync(request.Filter).ConfigureAwait(false);
                    List<DatabaseEntry> page = await persistence.DatabaseConnections.EnumerateAsync(request.MaxResults, request.Skip, request.Filter).ConfigureAwait(false);
                    foreach (DatabaseEntry entry in page)
                    {
                        response.Objects.Add(ToDatabaseContextRead(entry));
                    }

                    long remaining = Math.Max(0, totalRecords - request.Skip - page.Count);
                    response.TotalRecords = totalRecords;
                    response.RecordsRemaining = remaining;
                    response.EndOfResults = remaining == 0;
                    response.NextSkip = remaining == 0 ? null : (int?)(request.Skip + page.Count);
                    stopwatch.Stop();
                    response.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                    return (object)response;
                });
        }

        private static void RegisterGetTableContext(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_get_table_context",
                "Read persisted table-level context for one table, multiple tables, or a paged set of table contexts in a database. Use this after selecting a database and before writing SQL for tables whose purpose, caveats, common joins, or business meaning matter. Use tableId/tableIds from tablix_list_tables when possible; tableName/tableNames are accepted for convenience and resolved against crawled metadata. If includeEmpty is true, returned records include crawled tables even when no table context has been written yet. Table context complements schema geometry; still call tablix_discover_table for columns, keys, and indexes before querying.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID." },
                        tableId = new { type = "string", description = "Optional single table metadata ID." },
                        tableIds = new { type = "array", items = new { type = "string" }, description = "Optional list of table metadata IDs." },
                        tableName = new { type = "string", description = "Optional single table name." },
                        tableNames = new { type = "array", items = new { type = "string" }, description = "Optional list of table names." },
                        includeEmpty = new { type = "boolean", description = "Include crawled tables that do not yet have table context." },
                        maxResults = new { type = "integer", description = "Maximum table contexts to return when listing (1-1000, default 100)." },
                        skip = new { type = "integer", description = "Number of table contexts to skip when listing (default 0)." },
                        filter = new { type = "string", description = "Optional case-insensitive table or schema filter when listing." },
                        schema = new { type = "string", description = "Optional exact schema filter when listing." }
                    },
                    required = new[] { "databaseId" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_get_table_context invoked");
                    McpGetTableContextRequest request = ReadRequest<McpGetTableContextRequest>(args);
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    McpTableContextReadResponse response = new McpTableContextReadResponse
                    {
                        DatabaseId = request.DatabaseId,
                        MaxResults = request.MaxResults,
                        Skip = request.Skip
                    };

                    if (String.IsNullOrEmpty(request.DatabaseId))
                    {
                        response.Success = false;
                        response.Error = "databaseId is required";
                        return (object)response;
                    }

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                    {
                        response.Success = false;
                        response.Error = "Database '" + request.DatabaseId + "' not found";
                        return (object)response;
                    }

                    DatabaseDetail detail = await ReadDatabaseDetailAsync(persistence, crawlCache, entry).ConfigureAwait(false);
                    List<string> requestedTableIds = BuildRequestedTableIds(request, detail, response);

                    if (requestedTableIds.Count > 0)
                    {
                        foreach (string tableId in requestedTableIds)
                        {
                            TableDetail table = FindTableById(detail, tableId);
                            if (table == null)
                            {
                                response.MissingTableIds.Add(tableId);
                                continue;
                            }

                            TableContextRead context = await persistence.TableContexts.ReadAsync(request.DatabaseId, table.TableId).ConfigureAwait(false);
                            if (context == null && request.IncludeEmpty)
                                context = ToTableContextRead(request.DatabaseId, table);

                            if (context == null)
                                context = ToTableContextRead(request.DatabaseId, table);

                            response.Objects.Add(context);
                        }

                        response.Success = response.MissingTableIds.Count == 0 && response.MissingTableNames.Count == 0;
                        if (response.Objects.Count > 0)
                            response.Success = true;
                        response.TotalRecords = response.Objects.Count;
                        response.RecordsRemaining = 0;
                        response.EndOfResults = true;
                        stopwatch.Stop();
                        response.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                        return (object)response;
                    }

                    List<TableContextRead> contexts = request.IncludeEmpty
                        ? BuildAllTableContextReads(request.DatabaseId, detail)
                        : await persistence.TableContexts.EnumerateAsync(request.DatabaseId).ConfigureAwait(false);

                    List<TableContextRead> filtered = FilterTableContexts(contexts, request.Filter, request.Schema);
                    List<TableContextRead> page = filtered.Skip(request.Skip).Take(request.MaxResults).ToList();
                    long remaining = Math.Max(0, filtered.Count - request.Skip - page.Count);
                    response.Objects = page;
                    response.TotalRecords = filtered.Count;
                    response.RecordsRemaining = remaining;
                    response.EndOfResults = remaining == 0;
                    response.NextSkip = remaining == 0 ? null : (int?)(request.Skip + page.Count);
                    stopwatch.Stop();
                    response.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                    return (object)response;
                });
        }

        private static void RegisterUpdateContext(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_update_context",
                "General context update tool for backward compatibility. Prefer tablix_update_database_context for database-level context and tablix_update_table_context for table-level context. This tool accepts scope = Database or Table plus either one top-level update or an updates array for multiple entities. Use context updates only when the user asks to save/update context, when the workflow explicitly requires persisted analysis, or when refreshed schema proves saved context has wrong column names, wrong column types, or stale relationship guidance. Preserve human-provided facts, separate declared relationships from inferred ones, and include useful query patterns and caveats such as degraded crawl state or implicit relationships. Use append for incremental notes; use replace only when writing a complete curated context. Do not store secrets, raw result data, access tokens, credentials, or unsupported guesses as facts.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        scope = new { type = "string", description = "Context scope: Database or Table. Defaults to Database for backward compatibility." },
                        databaseId = new { type = "string", description = "Database entry ID" },
                        tableId = new { type = "string", description = "Table metadata ID when scope is Table." },
                        tableName = new { type = "string", description = "Table name when scope is Table and tableId is not known." },
                        context = new { type = "string", description = "Context text to save. Include concise purpose, important entities, declared relationships, inferred relationships clearly labeled as inferred, common query patterns, and caveats." },
                        mode = new { type = "string", description = "Update mode: replace or append (default replace). Prefer append for incremental findings unless replacing with a complete curated context." },
                        updates = new { type = "array", items = new { type = "object" }, description = "Optional batch updates. Each item may include scope, databaseId, tableId, tableName, context, and mode." }
                    }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_update_context invoked");
                    return (object)await HandleUpdateContextAsync(args, null, persistence, crawlCache).ConfigureAwait(false);
                });
        }

        private static void RegisterUpdateDatabaseContext(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_update_database_context",
                "Persist database-level context for one database or multiple databases. Use this after schema analysis, human instruction, or schema refresh proves the saved database context is stale. Database context should describe database purpose, major domains, important tables, declared relationships, inferred relationships clearly labeled as inferred, common query patterns, and caveats. Do not store secrets, credentials, raw query result data, or unsupported guesses. Use append for incremental notes; use replace only when writing a complete curated database context. For table-specific facts, use tablix_update_table_context instead.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID for a single update." },
                        context = new { type = "string", description = "Database-level context text." },
                        mode = new { type = "string", description = "Update mode: replace or append." },
                        updates = new { type = "array", items = new { type = "object" }, description = "Optional batch database-context updates. Each item includes databaseId, context, and optional mode." }
                    }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_update_database_context invoked");
                    return (object)await HandleUpdateContextAsync(args, ContextScopeEnum.Database, persistence, crawlCache).ConfigureAwait(false);
                });
        }

        private static void RegisterUpdateTableContext(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_update_table_context",
                "Persist table-level context for one table or multiple tables in a database. Use this when analysis produces table-specific durable guidance: table purpose, important columns, business meanings, join paths, filters, row caveats, common query patterns, or corrections to stale table context. Prefer tableId/tableIds from tablix_list_tables; tableName/tableNames are accepted when table IDs are not known. Do not store secrets, credentials, raw query result data, or unsupported guesses. Use append for incremental notes; use replace only when writing complete curated table context.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID." },
                        tableId = new { type = "string", description = "Table metadata ID for a single update." },
                        tableName = new { type = "string", description = "Table name for a single update when tableId is not known." },
                        context = new { type = "string", description = "Table-level context text." },
                        mode = new { type = "string", description = "Update mode: replace or append." },
                        updates = new { type = "array", items = new { type = "object" }, description = "Optional batch table-context updates. Each item includes tableId or tableName, context, and optional mode." }
                    }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_update_table_context invoked");
                    return (object)await HandleUpdateContextAsync(args, ContextScopeEnum.Table, persistence, crawlCache).ConfigureAwait(false);
                });
        }

        private static void RegisterDatabaseIntelligence(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_get_database_intelligence",
                "Get Tablix's schema-to-domain intelligence for one database: domain entities, inferred relationship candidates, ambiguity signals, context quality score, and an optional agent pack. Use this after tablix_discover_databases when you need a compact domain readout before answering or generating SQL. Treat inferred relationships as candidates unless saved context confirms them.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        includeAgentPack = new { type = "boolean", description = "Whether to include the markdown agent pack. Defaults to true." }
                    },
                    required = new[] { "databaseId" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_get_database_intelligence invoked");
                    McpDatabaseIntelligenceRequest request = ReadRequest<McpDatabaseIntelligenceRequest>(args);
                    if (String.IsNullOrWhiteSpace(request.DatabaseId))
                        return (object)new McpErrorResponse("databaseId is required");

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                        return (object)new McpErrorResponse("Database '" + request.DatabaseId + "' not found");

                    DatabaseDetail detail = await ReadDatabaseDetailAsync(persistence, crawlCache, entry).ConfigureAwait(false);
                    detail.Context = entry.Context;
                    DatabaseIntelligenceResponse response = DatabaseIntelligenceBuilder.Build(entry, detail, request.IncludeAgentPack);
                    return (object)response;
                });
        }

        private static void RegisterAgentPack(RegisterToolDelegate register, DatabaseDriverBase persistence, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_get_agent_pack",
                "Get MCP-ready agent instructions for one database, including the selected databaseId, safe discovery loop, major entities, declared and inferred relationships, ambiguity warnings, and useful starter questions. Use this to brief an agent before deeper schema inspection or query execution.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" }
                    },
                    required = new[] { "databaseId" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_get_agent_pack invoked");
                    McpDiscoverDatabaseRequest request = ReadRequest<McpDiscoverDatabaseRequest>(args);
                    if (String.IsNullOrWhiteSpace(request.DatabaseId))
                        return (object)new McpErrorResponse("databaseId is required");

                    DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(request.DatabaseId).ConfigureAwait(false);
                    if (entry == null)
                        return (object)new McpErrorResponse("Database '" + request.DatabaseId + "' not found");

                    DatabaseDetail detail = await ReadDatabaseDetailAsync(persistence, crawlCache, entry).ConfigureAwait(false);
                    detail.Context = entry.Context;
                    DatabaseIntelligenceResponse intelligence = DatabaseIntelligenceBuilder.Build(entry, detail, true);
                    return (object)intelligence.AgentPack;
                });
        }

        private static async Task<McpContextUpdateResponse> HandleUpdateContextAsync(
            object args,
            ContextScopeEnum? forcedScope,
            DatabaseDriverBase persistence,
            CrawlCache crawlCache)
        {
            McpUpdateContextRequest request = ReadRequest<McpUpdateContextRequest>(args);
            List<McpContextUpdateItemRequest> updates = BuildContextUpdateItems(request, forcedScope);
            McpContextUpdateResponse response = new McpContextUpdateResponse
            {
                TotalRecords = updates.Count
            };

            if (updates.Count == 0)
            {
                response.Success = false;
                response.Error = "At least one context update is required.";
                return response;
            }

            foreach (McpContextUpdateItemRequest update in updates)
            {
                McpContextUpdateItemResponse itemResponse = await ApplyContextUpdateAsync(update, persistence, crawlCache).ConfigureAwait(false);
                response.Objects.Add(itemResponse);
                if (itemResponse.Success)
                    response.Succeeded++;
                else
                    response.Failed++;
            }

            response.Success = response.Failed == 0;
            if (!response.Success)
                response.Error = response.Failed + " context update(s) failed.";

            if (response.Objects.Count == 1)
            {
                McpContextUpdateItemResponse only = response.Objects[0];
                response.Success = only.Success;
                response.Scope = only.Scope;
                response.DatabaseId = only.DatabaseId;
                response.TableId = only.TableId;
                response.TableName = only.TableName;
                response.Context = only.Context;
                response.Mode = only.Mode;
                response.Error = only.Error;
            }

            return response;
        }

        private static List<McpContextUpdateItemRequest> BuildContextUpdateItems(McpUpdateContextRequest request, ContextScopeEnum? forcedScope)
        {
            List<McpContextUpdateItemRequest> updates = new List<McpContextUpdateItemRequest>();
            if (request.Updates.Count > 0)
            {
                foreach (McpContextUpdateItemRequest update in request.Updates)
                {
                    McpContextUpdateItemRequest normalized = new McpContextUpdateItemRequest
                    {
                        Scope = forcedScope ?? update.Scope ?? request.Scope ?? ContextScopeEnum.Database,
                        DatabaseId = String.IsNullOrWhiteSpace(update.DatabaseId) ? request.DatabaseId : update.DatabaseId,
                        TableId = String.IsNullOrWhiteSpace(update.TableId) ? request.TableId : update.TableId,
                        TableName = String.IsNullOrWhiteSpace(update.TableName) ? request.TableName : update.TableName,
                        Context = update.Context ?? request.Context,
                        Mode = String.IsNullOrWhiteSpace(update.Mode) ? request.Mode : update.Mode
                    };
                    updates.Add(normalized);
                }
            }
            else
            {
                updates.Add(new McpContextUpdateItemRequest
                {
                    Scope = forcedScope ?? request.Scope ?? ContextScopeEnum.Database,
                    DatabaseId = request.DatabaseId,
                    TableId = request.TableId,
                    TableName = request.TableName,
                    Context = request.Context,
                    Mode = request.Mode
                });
            }

            return updates;
        }

        private static async Task<McpContextUpdateItemResponse> ApplyContextUpdateAsync(
            McpContextUpdateItemRequest update,
            DatabaseDriverBase persistence,
            CrawlCache crawlCache)
        {
            ContextScopeEnum scope = update.Scope ?? ContextScopeEnum.Database;
            if (scope == ContextScopeEnum.Table)
                return await ApplyTableContextUpdateAsync(update, persistence, crawlCache).ConfigureAwait(false);

            return await ApplyDatabaseContextUpdateAsync(update, persistence, crawlCache).ConfigureAwait(false);
        }

        private static async Task<McpContextUpdateItemResponse> ApplyDatabaseContextUpdateAsync(
            McpContextUpdateItemRequest update,
            DatabaseDriverBase persistence,
            CrawlCache crawlCache)
        {
            McpContextUpdateItemResponse response = new McpContextUpdateItemResponse
            {
                Scope = ContextScopeEnum.Database,
                DatabaseId = update.DatabaseId,
                Mode = NormalizeMode(update.Mode)
            };

            if (String.IsNullOrEmpty(update.DatabaseId))
                return FailContextUpdate(response, "databaseId is required");

            DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(update.DatabaseId).ConfigureAwait(false);
            if (entry == null)
                return FailContextUpdate(response, "Database '" + update.DatabaseId + "' not found");

            try
            {
                string context = await persistence.DatabaseContexts.UpsertAsync(entry.Id, update.Context, response.Mode, "mcp").ConfigureAwait(false);
                entry.Context = context;
                response.Context = context;

                DatabaseDetail cached = crawlCache.Get(update.DatabaseId);
                if (cached != null)
                    cached.Context = context;

                return response;
            }
            catch (Exception ex)
            {
                return FailContextUpdate(response, ex.Message);
            }
        }

        private static async Task<McpContextUpdateItemResponse> ApplyTableContextUpdateAsync(
            McpContextUpdateItemRequest update,
            DatabaseDriverBase persistence,
            CrawlCache crawlCache)
        {
            McpContextUpdateItemResponse response = new McpContextUpdateItemResponse
            {
                Scope = ContextScopeEnum.Table,
                DatabaseId = update.DatabaseId,
                TableId = update.TableId,
                TableName = update.TableName,
                Mode = NormalizeMode(update.Mode)
            };

            if (String.IsNullOrEmpty(update.DatabaseId))
                return FailContextUpdate(response, "databaseId is required");

            DatabaseEntry entry = await persistence.DatabaseConnections.ReadAsync(update.DatabaseId).ConfigureAwait(false);
            if (entry == null)
                return FailContextUpdate(response, "Database '" + update.DatabaseId + "' not found");

            DatabaseDetail detail = await ReadDatabaseDetailAsync(persistence, crawlCache, entry).ConfigureAwait(false);
            TableDetail table = ResolveTable(detail, update.TableId, update.TableName);
            if (table == null)
                return FailContextUpdate(response, "Table '" + (update.TableId ?? update.TableName) + "' not found in database '" + update.DatabaseId + "'");

            try
            {
                TableContextRead context = await persistence.TableContexts.UpsertAsync(
                    update.DatabaseId,
                    table.TableId,
                    update.Context,
                    response.Mode,
                    "mcp").ConfigureAwait(false);

                table.Context = context.Context;
                response.TableId = context.TableId;
                response.TableName = context.TableName;
                response.Context = context.Context;

                DatabaseDetail cached = crawlCache.Get(update.DatabaseId);
                TableDetail cachedTable = ResolveTable(cached, context.TableId, context.TableName);
                if (cachedTable != null)
                    cachedTable.Context = context.Context;

                return response;
            }
            catch (Exception ex)
            {
                return FailContextUpdate(response, ex.Message);
            }
        }

        private static McpContextUpdateItemResponse FailContextUpdate(McpContextUpdateItemResponse response, string error)
        {
            response.Success = false;
            response.Error = error;
            return response;
        }

        private static string NormalizeMode(string mode)
        {
            if (String.IsNullOrWhiteSpace(mode)) return "replace";
            return mode.Trim().ToLowerInvariant();
        }

        private static List<string> BuildRequestedDatabaseIds(McpGetDatabaseContextRequest request)
        {
            List<string> requestedIds = new List<string>();
            if (!String.IsNullOrWhiteSpace(request.DatabaseId))
                requestedIds.Add(request.DatabaseId);

            foreach (string databaseId in request.DatabaseIds)
            {
                if (!String.IsNullOrWhiteSpace(databaseId))
                    requestedIds.Add(databaseId);
            }

            return requestedIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static DatabaseContextRead ToDatabaseContextRead(DatabaseEntry entry)
        {
            return new DatabaseContextRead
            {
                DatabaseId = entry.Id,
                Name = entry.Name,
                Type = entry.Type.ToString(),
                Context = entry.Context
            };
        }

        private static async Task<DatabaseDetail> ReadDatabaseDetailAsync(DatabaseDriverBase persistence, CrawlCache crawlCache, DatabaseEntry entry)
        {
            DatabaseDetail detail = crawlCache.Get(entry.Id);
            if (detail == null)
                detail = await persistence.DatabaseMetadata.ReadDetailAsync(entry.Id).ConfigureAwait(false);
            if (detail == null)
            {
                detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                await persistence.DatabaseMetadata.SaveCrawlAsync(detail).ConfigureAwait(false);
            }

            return detail;
        }

        private static List<string> BuildRequestedTableIds(
            McpGetTableContextRequest request,
            DatabaseDetail detail,
            McpTableContextReadResponse response)
        {
            List<string> tableIds = new List<string>();
            if (!String.IsNullOrWhiteSpace(request.TableId))
                tableIds.Add(request.TableId);

            foreach (string tableId in request.TableIds)
            {
                if (!String.IsNullOrWhiteSpace(tableId))
                    tableIds.Add(tableId);
            }

            if (!String.IsNullOrWhiteSpace(request.TableName))
                AddTableNameSelection(request.TableName, detail, tableIds, response);

            foreach (string tableName in request.TableNames)
            {
                if (!String.IsNullOrWhiteSpace(tableName))
                    AddTableNameSelection(tableName, detail, tableIds, response);
            }

            return tableIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddTableNameSelection(
            string tableName,
            DatabaseDetail detail,
            List<string> tableIds,
            McpTableContextReadResponse response)
        {
            TableDetail table = FindTableByName(detail, tableName);
            if (table == null)
            {
                response.MissingTableNames.Add(tableName);
                return;
            }

            tableIds.Add(table.TableId);
        }

        private static List<TableContextRead> BuildAllTableContextReads(string databaseId, DatabaseDetail detail)
        {
            List<TableContextRead> contexts = new List<TableContextRead>();
            if (detail == null || detail.Tables == null) return contexts;

            foreach (TableDetail table in detail.Tables)
            {
                contexts.Add(ToTableContextRead(databaseId, table));
            }

            return contexts;
        }

        private static TableContextRead ToTableContextRead(string databaseId, TableDetail table)
        {
            return new TableContextRead
            {
                DatabaseId = databaseId,
                TableId = table.TableId,
                SchemaName = table.SchemaName,
                TableName = table.TableName,
                Context = table.Context,
                Source = null
            };
        }

        private static List<TableContextRead> FilterTableContexts(List<TableContextRead> contexts, string filter, string schema)
        {
            IEnumerable<TableContextRead> query = contexts;

            if (!String.IsNullOrWhiteSpace(schema))
            {
                query = query.Where(context =>
                    String.Equals(context.SchemaName, schema, StringComparison.OrdinalIgnoreCase));
            }

            if (!String.IsNullOrWhiteSpace(filter))
            {
                query = query.Where(context =>
                    ContainsIgnoreCase(context.TableName, filter) ||
                    ContainsIgnoreCase(context.SchemaName, filter) ||
                    ContainsIgnoreCase(context.Context, filter));
            }

            return query
                .OrderBy(context => context.SchemaName)
                .ThenBy(context => context.TableName)
                .ToList();
        }

        private static bool ContainsIgnoreCase(string value, string filter)
        {
            if (String.IsNullOrEmpty(value) || String.IsNullOrEmpty(filter)) return false;
            return value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static TableDetail ResolveTable(DatabaseDetail detail, string tableId, string tableName)
        {
            TableDetail table = FindTableById(detail, tableId);
            if (table != null) return table;
            return FindTableByName(detail, tableName);
        }

        private static TableDetail FindTableById(DatabaseDetail detail, string tableId)
        {
            if (detail == null || detail.Tables == null || String.IsNullOrWhiteSpace(tableId)) return null;

            foreach (TableDetail table in detail.Tables)
            {
                if (String.Equals(table.TableId, tableId, StringComparison.OrdinalIgnoreCase))
                    return table;
            }

            return null;
        }

        private static TableDetail FindTableByName(DatabaseDetail detail, string tableName)
        {
            if (detail == null || detail.Tables == null || String.IsNullOrWhiteSpace(tableName)) return null;

            string schema = null;
            string name = tableName;
            int separator = tableName.IndexOf(".", StringComparison.Ordinal);
            if (separator > 0 && separator < tableName.Length - 1)
            {
                schema = tableName.Substring(0, separator);
                name = tableName.Substring(separator + 1);
            }

            foreach (TableDetail table in detail.Tables)
            {
                bool nameMatches = String.Equals(table.TableName, name, StringComparison.OrdinalIgnoreCase);
                bool schemaMatches = schema == null || String.Equals(table.SchemaName, schema, StringComparison.OrdinalIgnoreCase);
                if (nameMatches && schemaMatches)
                    return table;
            }

            return null;
        }

        private static T ReadRequest<T>(object args) where T : new()
        {
            if (args == null) return new T();
            T request = Serializer.DeserializeObject<T>(args);
            return request == null ? new T() : request;
        }

        #endregion
    }
}
