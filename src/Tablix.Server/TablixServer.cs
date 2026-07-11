namespace Tablix.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using SwiftStack;
    using SwiftStack.Rest;
    using SwiftStack.Rest.OpenApi;
    using Voltaic;
    using WatsonWebserver.Core;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;
    using Tablix.Server.Handlers;
    using Tablix.Server.Mcp;
    using Constants = Tablix.Core.Helpers.Constants;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;
    using LoggingSettings = Tablix.Core.Settings.LoggingSettings;

    /// <summary>
    /// Main Tablix server.
    /// </summary>
    public class TablixServer
    {
        #region Private-Members

        private readonly string _SettingsFilename;
        private readonly string _Header = "[TablixServer] ";
        private SettingsManager _SettingsManager;
        private LoggingModule _Logging;
        private CrawlCache _CrawlCache;
        private SwiftStackApp _App;
        private McpHttpServer _McpServer;
        private DatabaseHandler _DatabaseHandler;
        private ChatHandler _ChatHandler;
        private SettingsHandler _SettingsHandler;
        private DateTime _StartTimeUtc;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsFilename">Path to settings file.</param>
        public TablixServer(string settingsFilename)
        {
            _SettingsFilename = settingsFilename ?? Constants.SettingsFilename;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public async Task StartAsync(CancellationToken token = default)
        {
            _StartTimeUtc = DateTime.UtcNow;
            Welcome();
            InitializeSettings();
            InitializeLogging();

            _Logging.Info(_Header + "starting Tablix v" + Constants.ProductVersion);

            InitializeCrawlCache();
            await CrawlAllDatabasesAsync().ConfigureAwait(false);
            InitializeRest();
            InitializeMcp();

            // Start REST
            Task restTask = Task.Run(() => _App.Rest.Run(token));
            string restUrl = "http://" + _SettingsManager.Settings.Rest.Hostname + ":" + _SettingsManager.Settings.Rest.Port;
            _Logging.Info(_Header + "REST API available at " + restUrl);
            _Logging.Info(_Header + "Swagger UI available at " + restUrl + "/swagger");

            // Start MCP
            Task mcpTask = Task.Run(() => _McpServer.StartAsync(token));
            _Logging.Info(_Header + "MCP server available at http://" + _SettingsManager.Settings.Rest.Hostname + ":" + _SettingsManager.Settings.Rest.McpPort + "/rpc");
        }

        #endregion

        #region Private-Methods

        private void Welcome()
        {
            Console.WriteLine(
                Constants.Logo + Environment.NewLine +
                Environment.NewLine +
                "Tablix v" + Constants.ProductVersion + Environment.NewLine +
                "Database discovery and query platform" +
                Environment.NewLine);
        }

        private void InitializeSettings()
        {
            _SettingsManager = new SettingsManager(_SettingsFilename);
            Console.WriteLine("Settings loaded from " + _SettingsFilename);
        }

        private void InitializeLogging()
        {
            LoggingSettings logSettings = _SettingsManager.Settings.Logging;

            List<SyslogLogging.SyslogServer> syslogServers = new List<SyslogLogging.SyslogServer>();

            if (logSettings.Servers != null && logSettings.Servers.Count > 0)
            {
                foreach (Core.Settings.SyslogServer server in logSettings.Servers)
                {
                    syslogServers.Add(
                        new SyslogLogging.SyslogServer
                        {
                            Hostname = server.Hostname,
                            Port = server.Port
                        }
                    );

                    Console.WriteLine("| syslog://" + server.Hostname + ":" + server.Port);
                }
            }

            if (syslogServers.Count > 0)
                _Logging = new LoggingModule(syslogServers);
            else
                _Logging = new LoggingModule();

            _Logging.Settings.EnableConsole = logSettings.ConsoleLogging;
            _Logging.Settings.EnableColors = logSettings.EnableColors;
            _Logging.Settings.MinimumSeverity = (Severity)logSettings.MinimumSeverity;

            if (logSettings.FileLogging)
            {
                if (!Directory.Exists(logSettings.LogDirectory))
                    Directory.CreateDirectory(logSettings.LogDirectory);

                _Logging.Settings.FileLogging = FileLoggingMode.FileWithDate;
                _Logging.Settings.LogFilename = Path.Combine(logSettings.LogDirectory, logSettings.LogFilename);
            }
        }

        private void InitializeCrawlCache()
        {
            _CrawlCache = new CrawlCache(
                logInfo: (msg) => _Logging.Info(_Header + msg),
                logWarn: (msg) => _Logging.Warn(_Header + msg)
            );
        }

        private async Task CrawlAllDatabasesAsync()
        {
            _Logging.Info(_Header + "crawling " + _SettingsManager.Settings.Databases.Count + " configured database(s)");
            await _CrawlCache.CrawlAllAsync(_SettingsManager.Settings.Databases).ConfigureAwait(false);
        }

        private void InitializeRest()
        {
            TablixSettings settings = _SettingsManager.Settings;

            _App = new SwiftStackApp(Constants.ProductName, true);
            RestApp rest = _App.Rest;
            rest.QuietStartup = true;
            rest.WebserverSettings.Hostname = settings.Rest.Hostname;
            rest.WebserverSettings.Port = settings.Rest.Port;
            rest.WebserverSettings.Ssl.Enable = settings.Rest.Ssl;

            // OpenAPI
            rest.UseOpenApi(openApiSettings =>
            {
                openApiSettings.Info = new OpenApiInfo
                {
                    Title = "Tablix API",
                    Version = Constants.ProductVersion,
                    Description = "Database discovery and query platform."
                };
                openApiSettings.Tags = new List<OpenApiTag>
                {
                    new OpenApiTag("Database", "Database management, crawl, and query operations"),
                    new OpenApiTag("Chat", "Model-backed database chat operations"),
                    new OpenApiTag("Settings", "Server settings operations"),
                    new OpenApiTag("Health", "Health check endpoints")
                };
                openApiSettings.SecuritySchemes = new Dictionary<string, OpenApiSecurityScheme>
                {
                    ["Bearer"] = OpenApiSecurityScheme.Bearer("token", "Bearer token authentication using an API key from tablix.json.")
                };
            });

            // Authentication
            rest.AuthenticationRoute = async (HttpContextBase ctx) =>
            {
                string authHeader = ctx.Request.Headers?[Constants.AuthorizationHeader];
                string token = null;

                if (!String.IsNullOrEmpty(authHeader) && authHeader.StartsWith(Constants.BearerPrefix, StringComparison.OrdinalIgnoreCase))
                    token = authHeader.Substring(Constants.BearerPrefix.Length).Trim();

                AuthResult result = new AuthResult();

                if (!String.IsNullOrEmpty(token) && settings.ApiKeys.Contains(token))
                {
                    result.AuthenticationResult = AuthenticationResultEnum.Success;
                }
                else
                {
                    result.AuthenticationResult = AuthenticationResultEnum.Invalid;
                }

                return result;
            };

            // Pre-routing: set JSON content type
            rest.PreRoutingRoute = async (HttpContextBase ctx) =>
            {
                ctx.Response.ContentType = Constants.JsonContentType;
            };

            // Exception route
            rest.ExceptionRoute = async (HttpContextBase ctx, Exception ex) =>
            {
                _Logging.Warn(_Header + "exception: " + ex.Message);

                int statusCode = 500;
                ApiErrorEnum errorType = ApiErrorEnum.InternalError;

                if (ex is KeyNotFoundException)
                {
                    statusCode = 404;
                    errorType = ApiErrorEnum.NotFound;
                }
                else if (ex is ArgumentException || ex is ArgumentNullException)
                {
                    statusCode = 400;
                    errorType = ApiErrorEnum.BadRequest;
                }
                else if (ex is UnauthorizedAccessException)
                {
                    statusCode = 401;
                    errorType = ApiErrorEnum.AuthenticationFailed;
                }
                else if (ex is InvalidOperationException)
                {
                    statusCode = 409;
                    errorType = ApiErrorEnum.Conflict;
                }

                ctx.Response.StatusCode = statusCode;
                ctx.Response.ContentType = Constants.JsonContentType;
                await ctx.Response.Send(Serializer.SerializeJson(new ApiErrorResponse(errorType, ex.Message), true)).ConfigureAwait(false);
            };

            // Register handlers
            _DatabaseHandler = new DatabaseHandler(_SettingsManager, _CrawlCache);
            _ChatHandler = new ChatHandler(_SettingsManager, _CrawlCache, _Logging);
            _SettingsHandler = new SettingsHandler(_SettingsManager);

            // Health (no auth)
            rest.Get("/", async (AppRequest r) => new { Name = Constants.ProductName, Version = Constants.ProductVersion, StartTimeUtc = _StartTimeUtc, Uptime = DateTime.UtcNow - _StartTimeUtc },
                api => api.WithTag("Health").WithSummary("Health check"), false);

            rest.Head("/", async (AppRequest r) => { r.Http.Response.StatusCode = 200; return null; },
                api => api.WithTag("Health").WithSummary("Health check (HEAD)"), false);

            // Database CRUD routes (require auth)
            rest.Get("/v1/database", _DatabaseHandler.ListDatabasesAsync,
                api => api.WithTag("Database").WithSummary("List all databases (paginated)")
                    .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum results (1-1000)", false))
                    .WithParameter(OpenApiParameterMetadata.Query("skip", "Records to skip", false))
                    .WithParameter(OpenApiParameterMetadata.Query("filter", "Filter by ID or name", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json<EnumerationResult<DatabaseSummary>>("Paginated redacted database list"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Get("/v1/database/{id}", _DatabaseHandler.GetDatabaseAsync,
                api => api.WithTag("Database").WithSummary("Get database details")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DatabaseReadDetail>("Redacted database detail with geometry"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Get("/v1/database/{id}/tables", _DatabaseHandler.ListTablesAsync,
                api => api.WithTag("Database").WithSummary("List database tables (paginated)")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum results (1-1000)", false))
                    .WithParameter(OpenApiParameterMetadata.Query("skip", "Records to skip", false))
                    .WithParameter(OpenApiParameterMetadata.Query("filter", "Filter by table or schema name", false))
                    .WithParameter(OpenApiParameterMetadata.Query("schema", "Filter by schema name", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DatabaseTableListResult>("Paginated table list"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Get("/v1/database/{id}/relationships", _DatabaseHandler.ListRelationshipsAsync,
                api => api.WithTag("Database").WithSummary("List database relationships (paginated)")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithParameter(OpenApiParameterMetadata.Query("maxResults", "Maximum results (1-1000)", false))
                    .WithParameter(OpenApiParameterMetadata.Query("skip", "Records to skip", false))
                    .WithParameter(OpenApiParameterMetadata.Query("filter", "Filter by table, column, or constraint name", false))
                    .WithParameter(OpenApiParameterMetadata.Query("schema", "Filter by source or target schema", false))
                    .WithParameter(OpenApiParameterMetadata.Query("includeInferred", "Reserved for inferred relationships; currently returns declared foreign keys only", false))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DatabaseRelationshipListResult>("Paginated relationship list"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post<DatabaseEntry>("/v1/database", _DatabaseHandler.AddDatabaseAsync,
                api => api.WithTag("Database").WithSummary("Add a new database entry")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<DatabaseEntry>("Database entry to add", true))
                    .WithResponse(201, OpenApiResponseMetadata.Json<DatabaseSummary>("Created database entry summary"))
                    .WithResponse(409, OpenApiResponseMetadata.Create("Conflict with existing database"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Put<DatabaseEntry>("/v1/database/{id}", _DatabaseHandler.UpdateDatabaseAsync,
                api => api.WithTag("Database").WithSummary("Update an existing database entry")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<DatabaseEntry>("Updated database entry", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DatabaseSummary>("Updated database entry summary"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post<ContextUpdateRequest>("/v1/database/{id}/context", _DatabaseHandler.UpdateDatabaseContextAsync,
                api => api.WithTag("Database").WithSummary("Update database context")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<ContextUpdateRequest>("Context update request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<object>("Updated context"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post<BuildContextRequest>("/v1/database/{id}/context/build", _ChatHandler.BuildContextAsync,
                api => api.WithTag("Database").WithSummary("Build database context using a model provider")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<BuildContextRequest>("Context build request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<BuildContextResponse>("Generated and persisted context"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database or provider not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Delete("/v1/database/{id}", _DatabaseHandler.DeleteDatabaseAsync,
                api => api.WithTag("Database").WithSummary("Delete a database entry")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithResponse(204, OpenApiResponseMetadata.Create("Deleted"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post("/v1/database/{id}/crawl", _DatabaseHandler.CrawlDatabaseAsync,
                api => api.WithTag("Database").WithSummary("Re-crawl database schema")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Json<DatabaseDetail>("Crawl result"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post("/v1/database/{id}/crawl/stream", _DatabaseHandler.CrawlDatabaseStreamAsync,
                api => api.WithTag("Database").WithSummary("Re-crawl database schema with streamed progress")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithResponse(200, OpenApiResponseMetadata.Text("Server-sent event stream with crawl status events"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post<QueryRequest>("/v1/database/{id}/query", _DatabaseHandler.ExecuteQueryAsync,
                api => api.WithTag("Database").WithSummary("Execute a SQL query")
                    .WithParameter(OpenApiParameterMetadata.Path("id", "Database entry ID"))
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<QueryRequest>("SQL query to execute", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<QueryResult>("Query result"))
                    .WithResponse(403, OpenApiResponseMetadata.Create("Query type not permitted"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Get("/v1/chat/options", _ChatHandler.GetOptionsAsync,
                api => api.WithTag("Chat").WithSummary("Get chat options")
                    .WithResponse(200, OpenApiResponseMetadata.Json<ChatOptionsResponse>("Chat database and provider options"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post<ChatRequest>("/v1/chat", _ChatHandler.ChatAsync,
                api => api.WithTag("Chat").WithSummary("Send a non-streaming chat message")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<ChatRequest>("Chat request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<ChatResponseResult>("Chat response"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database or provider not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Post<ChatRequest>("/v1/chat/stream", _ChatHandler.ChatStreamAsync,
                api => api.WithTag("Chat").WithSummary("Send a streaming chat message")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<ChatRequest>("Chat request", true))
                    .WithResponse(200, OpenApiResponseMetadata.Text("Server-sent event stream with chat tokens and telemetry"))
                    .WithResponse(404, OpenApiResponseMetadata.NotFound("Database or provider not found"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Get("/v1/settings", _SettingsHandler.GetSettingsAsync,
                api => api.WithTag("Settings").WithSummary("Read redacted server settings")
                    .WithResponse(200, OpenApiResponseMetadata.Json<SettingsReadResponse>("Redacted server settings"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);

            rest.Put<SettingsUpdateRequest>("/v1/settings", _SettingsHandler.UpdateSettingsAsync,
                api => api.WithTag("Settings").WithSummary("Update server settings")
                    .WithRequestBody(OpenApiRequestBodyMetadata.Json<SettingsUpdateRequest>("Settings update", true))
                    .WithResponse(200, OpenApiResponseMetadata.Json<SettingsReadResponse>("Updated redacted server settings"))
                    .WithSecurity("Bearer", Array.Empty<string>()), true);
        }

        private void InitializeMcp()
        {
            TablixSettings settings = _SettingsManager.Settings;

            _McpServer = new McpHttpServer(settings.Rest.Hostname, settings.Rest.McpPort);
            _McpServer.ServerName = Constants.ProductName;
            _McpServer.ServerVersion = Constants.ProductVersion;

            McpToolRegistrar.RegisterAll(
                _McpServer.RegisterTool,
                _SettingsManager,
                _CrawlCache,
                (msg) => _Logging.Debug(_Header + msg));
        }

        #endregion
    }
}
