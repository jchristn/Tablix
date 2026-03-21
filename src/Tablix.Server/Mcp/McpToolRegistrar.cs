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

            RegisterDiscoverDatabases(register, settingsManager, logDebug);
            RegisterDiscoverDatabase(register, settingsManager, crawlCache, logDebug);
            RegisterListTables(register, settingsManager, crawlCache, logDebug);
            RegisterDiscoverTable(register, settingsManager, crawlCache, logDebug);
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

        private static void RegisterDiscoverDatabases(RegisterToolDelegate register, SettingsManager settingsManager, Action<string> logDebug)
        {
            register(
                "tablix_discover_databases",
                "Step 1: Start here. Lists all configured databases with their connection details and user-supplied context. After identifying a database, call tablix_list_tables to see its tables, then tablix_discover_table for the geometry of each table you need. Supports pagination via maxResults and skip, and optional filtering by database ID or name.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        maxResults = new { type = "integer", description = "Maximum number of results to return (1-1000, default 100)" },
                        skip = new { type = "integer", description = "Number of records to skip (default 0)" },
                        filter = new { type = "string", description = "Filter string to match against database ID or name" }
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

                    return (object)result;
                });
        }

        private static void RegisterDiscoverDatabase(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_discover_database",
                "Returns the full schema geometry for every table in a database at once (tables, columns, primary keys, foreign keys, indexes). This can produce large responses — prefer the targeted workflow: tablix_list_tables to see available tables, then tablix_discover_table for only the tables you need.",
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
                    logDebug?.Invoke("[MCP] tablix_discover_database invoked");

                    string databaseId = null;
                    if (args.HasValue && args.Value.TryGetProperty("databaseId", out JsonElement idEl))
                        databaseId = idEl.GetString();

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

                    return (object)detail;
                });
        }

        private static void RegisterListTables(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_list_tables",
                "Step 2: After identifying a database with tablix_discover_databases, call this to list its tables. Returns each table's name, schema, and column count. Then call tablix_discover_table for the full geometry of each table you need.",
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
                    logDebug?.Invoke("[MCP] tablix_list_tables invoked");

                    string databaseId = null;
                    if (args.HasValue && args.Value.TryGetProperty("databaseId", out JsonElement idEl))
                        databaseId = idEl.GetString();

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

                    var tables = detail.Tables.Select(t => new { t.SchemaName, t.TableName, Columns = t.Columns.Count }).ToList();

                    return (object)new
                    {
                        DatabaseId = databaseId,
                        Context = detail.Context,
                        IsCrawled = detail.IsCrawled,
                        TableCount = detail.Tables.Count,
                        Tables = tables
                    };
                });
        }

        private static void RegisterDiscoverTable(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_discover_table",
                "Step 3: After listing tables with tablix_list_tables, call this to get full geometry for a specific table — columns, data types, primary keys, foreign keys, and indexes. Call once per table you need to understand before writing queries.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        tableName = new { type = "string", description = "Table name to retrieve geometry for" }
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

        private static void RegisterExecuteQuery(RegisterToolDelegate register, SettingsManager settingsManager, CrawlCache crawlCache, Action<string> logDebug)
        {
            register(
                "tablix_execute_query",
                "Execute a SQL query against a specific database. The query must be a single statement (no semicolons) and the statement type must be in the database's AllowedQueries list.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        query = new { type = "string", description = "SQL query to execute" }
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
                "Update the user-supplied context description for a database. The context helps AI agents understand what the database contains, how its tables relate, and what queries are useful.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        databaseId = new { type = "string", description = "Database entry ID" },
                        context = new { type = "string", description = "New context description for the database" }
                    },
                    required = new[] { "databaseId", "context" }
                },
                async (args) =>
                {
                    logDebug?.Invoke("[MCP] tablix_update_context invoked");

                    string databaseId = null;
                    string context = null;

                    if (args.HasValue)
                    {
                        if (args.Value.TryGetProperty("databaseId", out JsonElement idEl)) databaseId = idEl.GetString();
                        if (args.Value.TryGetProperty("context", out JsonElement ctxEl)) context = ctxEl.GetString();
                    }

                    if (String.IsNullOrEmpty(databaseId))
                        return (object)new { Success = false, Error = "databaseId is required" };

                    DatabaseEntry entry = settingsManager.GetDatabase(databaseId);
                    if (entry == null)
                        return (object)new { Success = false, Error = "Database '" + databaseId + "' not found" };

                    entry.Context = context;

                    try
                    {
                        settingsManager.UpdateDatabase(entry);

                        // Update the cached crawl detail so the dashboard reflects the new context
                        DatabaseDetail cached = crawlCache.Get(databaseId);
                        if (cached != null)
                        {
                            cached.Context = context;
                        }

                        return (object)new { Success = true, DatabaseId = databaseId, Context = context };
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
