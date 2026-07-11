namespace Tablix.Server.Mcp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// Delegate matching the RegisterTool signature of McpHttpServer.
    /// </summary>
    public delegate void RegisterToolDelegate(
        string name,
        string description,
        object inputSchema,
        Func<JsonElement?, Task<object>> handler);

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
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="crawlCache">Crawl cache.</param>
        /// <param name="logDebug">Optional debug logging delegate.</param>
        public static void RegisterAll(
            RegisterToolDelegate register,
            SettingsManager settingsManager,
            CrawlCache crawlCache,
            Action<string> logDebug = null)
        {
            if (register == null) throw new ArgumentNullException(nameof(register));
            if (settingsManager == null) throw new ArgumentNullException(nameof(settingsManager));
            if (crawlCache == null) throw new ArgumentNullException(nameof(crawlCache));

            RegisterDiscoverDatabases(register, settingsManager, crawlCache, logDebug);
            RegisterDiscoverDatabase(register, settingsManager, crawlCache, logDebug);
            RegisterListTables(register, settingsManager, crawlCache, logDebug);
            RegisterDiscoverTable(register, settingsManager, crawlCache, logDebug);
            RegisterListRelationships(register, settingsManager, crawlCache, logDebug);
            RegisterExecuteQuery(register, settingsManager, crawlCache, logDebug);
            RegisterUpdateContext(register, settingsManager, crawlCache, logDebug);
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

        private static void RegisterDiscoverDatabases(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
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

                    int maxResults = 100;
                    int skip = 0;
                    string filter = null;

                    if (args.HasValue)
                    {
                        if (args.Value.TryGetProperty("maxResults", out JsonElement maxEl)) maxResults = Math.Clamp(maxEl.GetInt32(), 1, 1000);
                        if (args.Value.TryGetProperty("skip", out JsonElement skipEl)) skip = Math.Max(skipEl.GetInt32(), 0);
                        if (args.Value.TryGetProperty("filter", out JsonElement filterEl)) filter = filterEl.GetString();
                    }

                    Stopwatch stopwatch = Stopwatch.StartNew();
                    TablixSettings settings = settingsManager.Settings;
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
                        .Select(d => DatabaseSummary.From(d, crawlCache.Get(d.Id)))
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

                    return (object)result;
                });
        }

        private static void RegisterDiscoverDatabase(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
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

                    string databaseId = null;
                    int? maxTables = null;
                    int skip = 0;
                    if (args.HasValue && args.Value.TryGetProperty("databaseId", out JsonElement idEl))
                        databaseId = idEl.GetString();
                    if (args.HasValue && args.Value.TryGetProperty("maxTables", out JsonElement maxEl))
                        maxTables = Math.Clamp(maxEl.GetInt32(), 1, 1000);
                    if (args.HasValue && args.Value.TryGetProperty("skip", out JsonElement skipEl))
                        skip = Math.Max(skipEl.GetInt32(), 0);

                    if (String.IsNullOrEmpty(databaseId))
                        return (object)new { Error = "databaseId is required" };

                    DatabaseEntry entry = settingsManager.GetDatabase(databaseId);
                    if (entry == null)
                        return (object)new { Error = "Database '" + databaseId + "' not found" };

                    DatabaseDetail detail = crawlCache.Get(databaseId);
                    if (detail == null)
                    {
                        // Attempt a crawl
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                    }

                    if (maxTables.HasValue)
                    {
                        List<TableDetail> orderedTables = detail.Tables
                            .OrderBy(t => t.SchemaName)
                            .ThenBy(t => t.TableName)
                            .ToList();
                        List<TableDetail> page = orderedTables.Skip(skip).Take(maxTables.Value).ToList();
                        long remaining = Math.Max(0, orderedTables.Count - skip - page.Count);

                        return (object)new
                        {
                            detail.DatabaseId,
                            detail.Type,
                            detail.DatabaseName,
                            detail.Schema,
                            detail.Context,
                            detail.CrawledUtc,
                            detail.IsCrawled,
                            detail.CrawlError,
                            MaxResults = maxTables.Value,
                            Skip = skip,
                            TotalRecords = orderedTables.Count,
                            RecordsRemaining = remaining,
                            EndOfResults = remaining == 0,
                            NextSkip = remaining == 0 ? null : (int?)(skip + page.Count),
                            Tables = page
                        };
                    }

                    return (object)detail;
                });
        }

        private static void RegisterListTables(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
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

                    string databaseId = null;
                    int maxResults = 100;
                    int skip = 0;
                    string filter = null;
                    string schema = null;

                    if (args.HasValue && args.Value.TryGetProperty("databaseId", out JsonElement idEl))
                        databaseId = idEl.GetString();
                    if (args.HasValue && args.Value.TryGetProperty("maxResults", out JsonElement maxEl))
                        maxResults = Math.Clamp(maxEl.GetInt32(), 1, 1000);
                    if (args.HasValue && args.Value.TryGetProperty("skip", out JsonElement skipEl))
                        skip = Math.Max(skipEl.GetInt32(), 0);
                    if (args.HasValue && args.Value.TryGetProperty("filter", out JsonElement filterEl))
                        filter = filterEl.GetString();
                    if (args.HasValue && args.Value.TryGetProperty("schema", out JsonElement schemaEl))
                        schema = schemaEl.GetString();

                    if (String.IsNullOrEmpty(databaseId))
                        return (object)new { Error = "databaseId is required" };

                    DatabaseEntry entry = settingsManager.GetDatabase(databaseId);
                    if (entry == null)
                        return (object)new { Error = "Database '" + databaseId + "' not found" };

                    DatabaseDetail detail = crawlCache.Get(databaseId);
                    if (detail == null)
                    {
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                    }

                    return (object)SchemaProjection.CreateTableListResult(
                        databaseId,
                        detail,
                        maxResults,
                        skip,
                        filter,
                        schema);
                });
        }

        private static void RegisterDiscoverTable(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
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

                    string databaseId = null;
                    string tableName = null;

                    if (args.HasValue)
                    {
                        if (args.Value.TryGetProperty("databaseId", out JsonElement idEl)) databaseId = idEl.GetString();
                        if (args.Value.TryGetProperty("tableName", out JsonElement tblEl)) tableName = tblEl.GetString();
                    }

                    if (String.IsNullOrEmpty(databaseId))
                        return (object)new { Error = "databaseId is required" };

                    if (String.IsNullOrEmpty(tableName))
                        return (object)new { Error = "tableName is required" };

                    DatabaseEntry entry = settingsManager.GetDatabase(databaseId);
                    if (entry == null)
                        return (object)new { Error = "Database '" + databaseId + "' not found" };

                    DatabaseDetail detail = crawlCache.Get(databaseId);
                    if (detail == null)
                    {
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                    }

                    TableDetail table = detail.Tables.FirstOrDefault(t =>
                        String.Equals(t.TableName, tableName, StringComparison.OrdinalIgnoreCase));

                    if (table == null)
                        return (object)new { Error = "Table '" + tableName + "' not found in database '" + databaseId + "'" };

                    return (object)new
                    {
                        DatabaseId = databaseId,
                        Context = detail.Context,
                        Table = table
                    };
                });
        }

        private static void RegisterListRelationships(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
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

                    string databaseId = null;
                    int maxResults = 100;
                    int skip = 0;
                    string filter = null;
                    string schema = null;
                    bool includeInferred = false;

                    if (args.HasValue)
                    {
                        if (args.Value.TryGetProperty("databaseId", out JsonElement idEl)) databaseId = idEl.GetString();
                        if (args.Value.TryGetProperty("maxResults", out JsonElement maxEl)) maxResults = Math.Clamp(maxEl.GetInt32(), 1, 1000);
                        if (args.Value.TryGetProperty("skip", out JsonElement skipEl)) skip = Math.Max(skipEl.GetInt32(), 0);
                        if (args.Value.TryGetProperty("filter", out JsonElement filterEl)) filter = filterEl.GetString();
                        if (args.Value.TryGetProperty("schema", out JsonElement schemaEl)) schema = schemaEl.GetString();
                        if (args.Value.TryGetProperty("includeInferred", out JsonElement includeEl)) includeInferred = includeEl.GetBoolean();
                    }

                    if (String.IsNullOrEmpty(databaseId))
                        return (object)new { Error = "databaseId is required" };

                    DatabaseEntry entry = settingsManager.GetDatabase(databaseId);
                    if (entry == null)
                        return (object)new { Error = "Database '" + databaseId + "' not found" };

                    DatabaseDetail detail = crawlCache.Get(databaseId);
                    if (detail == null)
                    {
                        detail = await crawlCache.CrawlOneAsync(entry).ConfigureAwait(false);
                    }

                    return (object)SchemaProjection.CreateRelationshipListResult(
                        databaseId,
                        detail,
                        maxResults,
                        skip,
                        filter,
                        schema,
                        includeInferred);
                });
        }

        private static void RegisterExecuteQuery(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_execute_query",
                "Execute one SQL statement against a database after discovering enough schema to be confident. Use this tool to answer user requests for actual data or requested database changes, including phrases like show me, how many, count, list, find, total, average, latest, top, summarize, add, update, or delete. Do not merely provide SQL when the user asks for an answer or action and the statement type is allowed; run the permitted query and return the result. The query must contain no semicolons and its statement type must be allowed by the database's AllowedQueries from tablix_discover_databases. Prefer SELECT for exploration. Never run write statements unless the user explicitly asks for the change and AllowedQueries permits that statement type. Check AllowedQueries first. For row-reading queries, project only needed columns and include sensible limits when exploring row data; aggregate queries such as COUNT do not need a LIMIT. Validate table and column names with tablix_discover_table before querying. If execution fails because of a bad or unknown column, missing column, or column type mismatch, refresh schema by re-discovering the relevant table or database before retrying. If refreshed schema proves saved Context has wrong column names, wrong column types, or stale relationship guidance, update Context with tablix_update_context.",
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

                    string databaseId = null;
                    string query = null;

                    if (args.HasValue)
                    {
                        if (args.Value.TryGetProperty("databaseId", out JsonElement idEl)) databaseId = idEl.GetString();
                        if (args.Value.TryGetProperty("query", out JsonElement queryEl)) query = queryEl.GetString();
                    }

                    if (String.IsNullOrEmpty(databaseId))
                        return (object)new QueryResult { Success = false, Error = "databaseId is required" };

                    if (String.IsNullOrEmpty(query))
                        return (object)new QueryResult { Success = false, Error = "query is required" };

                    DatabaseEntry entry = settingsManager.GetDatabase(databaseId);
                    if (entry == null)
                        return (object)new QueryResult { Success = false, DatabaseId = databaseId, Error = "Database '" + databaseId + "' not found" };

                    // Validate query against allowed types
                    string validationError = QueryValidator.Validate(query, entry.AllowedQueries);
                    if (validationError != null)
                        return (object)new QueryResult { Success = false, DatabaseId = databaseId, Error = validationError };

                    try
                    {
                        IDatabaseCrawler crawler = CrawlerFactory.Create(entry.Type);
                        QueryResult result = await crawler.ExecuteQueryAsync(entry, query).ConfigureAwait(false);
                        return (object)result;
                    }
                    catch (Exception ex)
                    {
                        return (object)new QueryResult { Success = false, DatabaseId = databaseId, Error = ex.Message };
                    }
                });
        }

        private static void RegisterUpdateContext(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_update_context",
                "Persist database context back to Tablix settings after schema analysis or explicit user instruction. Use this only when the user asks to update/save context, when the workflow clearly requires persisting an analyzed summary, or when refreshed schema proves saved context has wrong column names, wrong column types, or stale relationship guidance. Preserve human-provided facts, separate declared relationships from inferred ones, and include useful query patterns and caveats such as degraded crawl state or implicit relationships. Use append for incremental notes; use replace only when writing a complete updated context. Do not store secrets, raw result data, or unsupported guesses as facts.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        context = new { type = "string", description = "Context text to save. Include concise business purpose, important tables, declared relationships, inferred relationships clearly labeled as inferred, and common query patterns." },
                        mode = new { type = "string", description = "Update mode: replace or append (default replace). Prefer append for incremental findings unless replacing with a complete curated context." }
                    },
                    required = new[] { "databaseId", "context" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_update_context invoked");

                    string databaseId = null;
                    string context = null;
                    string mode = "replace";

                    if (args.HasValue)
                    {
                        if (args.Value.TryGetProperty("databaseId", out JsonElement idEl)) databaseId = idEl.GetString();
                        if (args.Value.TryGetProperty("context", out JsonElement ctxEl)) context = ctxEl.GetString();
                        if (args.Value.TryGetProperty("mode", out JsonElement modeEl)) mode = modeEl.GetString();
                    }

                    if (String.IsNullOrEmpty(databaseId))
                        return (object)new { Success = false, Error = "databaseId is required" };

                    DatabaseEntry entry = settingsManager.GetDatabase(databaseId);
                    if (entry == null)
                        return (object)new { Success = false, Error = "Database '" + databaseId + "' not found" };

                    if (String.IsNullOrWhiteSpace(mode))
                        mode = "replace";

                    if (String.Equals(mode, "append", StringComparison.OrdinalIgnoreCase))
                    {
                        if (String.IsNullOrEmpty(entry.Context))
                            entry.Context = context;
                        else if (!String.IsNullOrEmpty(context))
                            entry.Context = entry.Context.TrimEnd() + Environment.NewLine + Environment.NewLine + context;
                    }
                    else if (String.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase))
                    {
                        entry.Context = context;
                    }
                    else
                    {
                        return (object)new { Success = false, DatabaseId = databaseId, Error = "Unsupported context update mode '" + mode + "'" };
                    }

                    try
                    {
                        settingsManager.UpdateDatabase(entry);

                        // Update the cached crawl detail so the dashboard reflects the new context
                        DatabaseDetail cached = crawlCache.Get(databaseId);
                        if (cached != null)
                        {
                            cached.Context = entry.Context;
                        }

                        return (object)new { Success = true, DatabaseId = databaseId, Context = entry.Context, Mode = mode.ToLowerInvariant() };
                    }
                    catch (Exception ex)
                    {
                        return (object)new { Success = false, DatabaseId = databaseId, Error = ex.Message };
                    }
                });
        }

        #endregion
    }
}
