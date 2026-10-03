namespace Tablix.Core.Observability
{
    /// <summary>
    /// Every telemetry name Tablix emits: the meter and activity source names, metric instrument names, span names,
    /// attribute (label) keys, and the bounded label values. These strings are public contract consumed by Grafana
    /// dashboards and alert rules, so treat a rename as a breaking change.
    /// Thread safety: constants only; safe for concurrent use.
    /// </summary>
    public static class TelemetryNames
    {
        #region Sources

        /// <summary>
        /// Name of the <see cref="System.Diagnostics.Metrics.Meter"/> that carries every Tablix application metric.
        /// </summary>
        public const string MeterName = "Tablix";

        /// <summary>
        /// Name of the <see cref="System.Diagnostics.ActivitySource"/> that carries every Tablix application span.
        /// </summary>
        public const string ActivitySourceName = "Tablix";

        /// <summary>
        /// Name of Watson's built-in meter and activity source (HTTP server metrics and per-request spans).
        /// </summary>
        public const string WatsonSourceName = "Watson";

        /// <summary>
        /// Name of the .NET HTTP client meter (outbound http.client.* metrics for model provider calls).
        /// </summary>
        public const string HttpClientMeterName = "System.Net.Http";

        #endregion

        #region Metric-Names

        /// <summary>Counter: schema crawl jobs by trigger, database system, and outcome.</summary>
        public const string MetricCrawlJobs = "tablix.crawl.jobs";

        /// <summary>Histogram (s): end-to-end schema crawl job duration.</summary>
        public const string MetricCrawlDuration = "tablix.crawl.duration";

        /// <summary>Histogram (s): duration of each crawl pipeline stage, including queued.</summary>
        public const string MetricCrawlStageDuration = "tablix.crawl.stage.duration";

        /// <summary>Counter: crawl pipeline stage completions by stage and outcome.</summary>
        public const string MetricCrawlStageEvents = "tablix.crawl.stage.events";

        /// <summary>Observable gauge (s): Unix time of the last successful crawl, by database system.</summary>
        public const string MetricCrawlLastSuccess = "tablix.crawl.last_success";

        /// <summary>Counter: tables discovered by successful crawls.</summary>
        public const string MetricCrawlTables = "tablix.crawl.tables";

        /// <summary>UpDownCounter: crawl jobs currently running.</summary>
        public const string MetricCrawlActive = "tablix.crawl.active";

        /// <summary>Counter: crawl cache lookups by result (hit or miss).</summary>
        public const string MetricCrawlCacheLookups = "tablix.crawl_cache.lookups";

        /// <summary>Observable gauge: crawl cache entries by state (crawled or degraded).</summary>
        public const string MetricCrawlCacheEntries = "tablix.crawl_cache.entries";

        /// <summary>Counter: operations against user-configured databases by system, operation, and outcome.</summary>
        public const string MetricDbClientOperations = "tablix.db.client.operations";

        /// <summary>Histogram (s): duration of operations against user-configured databases.</summary>
        public const string MetricDbClientDuration = "tablix.db.client.operation.duration";

        /// <summary>UpDownCounter: operations against user-configured databases currently in flight.</summary>
        public const string MetricDbClientActive = "tablix.db.client.active";

        /// <summary>Counter: SQL query executions by source, statement type, and outcome (including rejected).</summary>
        public const string MetricQueryExecutions = "tablix.query.executions";

        /// <summary>Histogram (s): SQL query execution duration including validation.</summary>
        public const string MetricQueryDuration = "tablix.query.duration";

        /// <summary>Histogram ({row}): rows returned by successful queries.</summary>
        public const string MetricQueryRows = "tablix.query.rows";

        /// <summary>Counter: schema refreshes triggered by schema-related chat query failures.</summary>
        public const string MetricQuerySchemaRefreshes = "tablix.query.schema_refreshes";

        /// <summary>Counter: chat requests by mode, execution path, and outcome.</summary>
        public const string MetricChatRequests = "tablix.chat.requests";

        /// <summary>Histogram (s): end-to-end chat request duration.</summary>
        public const string MetricChatDuration = "tablix.chat.duration";

        /// <summary>Histogram (s): duration of each chat workflow stage.</summary>
        public const string MetricChatStageDuration = "tablix.chat.stage.duration";

        /// <summary>Counter: chat tool calls by tool, phase, and outcome.</summary>
        public const string MetricChatToolCalls = "tablix.chat.tool_calls";

        /// <summary>Histogram (s): chat tool call duration.</summary>
        public const string MetricChatToolCallDuration = "tablix.chat.tool_call.duration";

        /// <summary>Histogram (s): time to first streamed token from a model provider.</summary>
        public const string MetricChatTimeToFirstToken = "tablix.chat.time_to_first_token";

        /// <summary>Counter: model-generated context builds by scope and outcome.</summary>
        public const string MetricContextBuilds = "tablix.context.builds";

        /// <summary>Histogram (s): model-generated context build duration.</summary>
        public const string MetricContextBuildDuration = "tablix.context.build.duration";

        /// <summary>Histogram (s): duration of each context build stage (queued, inference, persist).</summary>
        public const string MetricContextBuildStageDuration = "tablix.context.build.stage.duration";

        /// <summary>UpDownCounter: context build concurrency slots currently held.</summary>
        public const string MetricContextBuildSlotsInUse = "tablix.context.build.slots.in_use";

        /// <summary>Counter: context writes by scope, source, and outcome.</summary>
        public const string MetricContextUpdates = "tablix.context.updates";

        /// <summary>Counter: model provider requests by provider, operation, and outcome.</summary>
        public const string MetricModelRequests = "tablix.model.requests";

        /// <summary>Histogram (s): model provider request duration.</summary>
        public const string MetricModelRequestDuration = "tablix.model.request.duration";

        /// <summary>Counter ({token}): model tokens by provider and token type.</summary>
        public const string MetricModelTokens = "tablix.model.tokens";

        /// <summary>Counter: model provider health state transitions.</summary>
        public const string MetricModelHealthTransitions = "tablix.model.health.transitions";

        /// <summary>Observable gauge (s): Unix time the health monitor last completed a scheduling cycle.</summary>
        public const string MetricModelHealthLastCycle = "tablix.model.health.last_cycle";

        /// <summary>Observable gauge: configured model providers by health state.</summary>
        public const string MetricModelProviders = "tablix.model.providers";

        /// <summary>Counter: MCP tool calls by tool and outcome.</summary>
        public const string MetricMcpToolCalls = "tablix.mcp.tool.calls";

        /// <summary>Histogram (s): MCP tool call duration.</summary>
        public const string MetricMcpToolDuration = "tablix.mcp.tool.duration";

        /// <summary>UpDownCounter: MCP tool calls currently in flight.</summary>
        public const string MetricMcpToolActive = "tablix.mcp.tool.active";

        /// <summary>Counter: Tablix persistence (internal SQLite store) operations by kind and outcome.</summary>
        public const string MetricPersistenceOperations = "tablix.persistence.operations";

        /// <summary>Histogram (s): persistence operation duration, excluding the lock wait.</summary>
        public const string MetricPersistenceDuration = "tablix.persistence.operation.duration";

        /// <summary>Histogram (s): time spent waiting for the single persistence operation slot.</summary>
        public const string MetricPersistenceLockWait = "tablix.persistence.lock.wait";

        /// <summary>UpDownCounter: persistence operations waiting for the operation slot.</summary>
        public const string MetricPersistenceLockWaiting = "tablix.persistence.lock.waiting";

        /// <summary>Counter: handled errors by component and error type.</summary>
        public const string MetricErrors = "tablix.errors";

        /// <summary>Counter: settings updates by outcome.</summary>
        public const string MetricSettingsUpdates = "tablix.settings.updates";

        /// <summary>Observable gauge: build information (always 1), labeled by version and runtime.</summary>
        public const string MetricBuildInfo = "tablix.build.info";

        /// <summary>Observable gauge: safe boolean configuration flags (1 enabled, 0 disabled).</summary>
        public const string MetricConfigFlag = "tablix.config.flag";

        #endregion

        #region Span-Names

        /// <summary>Root span for server startup.</summary>
        public const string SpanStartup = "tablix.startup";

        /// <summary>Root span for a batch of crawls (for example the startup crawl).</summary>
        public const string SpanCrawlBatch = "crawl.batch";

        /// <summary>Span for one crawl pipeline job.</summary>
        public const string SpanCrawlJob = "crawl.job";

        /// <summary>Span for one chat request workflow.</summary>
        public const string SpanChat = "chat";

        /// <summary>Span for one model-generated context build.</summary>
        public const string SpanContextBuild = "context.build";

        /// <summary>Span for one table inside a table context build.</summary>
        public const string SpanContextTable = "context.table";

        /// <summary>Span for one model provider health check.</summary>
        public const string SpanHealthCheck = "health_check";

        /// <summary>Server span name prefix for one MCP tool call ("tools/call {tool}").</summary>
        public const string SpanMcpToolCallPrefix = "tools/call ";

        /// <summary>Span name prefix for one chat tool execution ("execute_tool {tool}").</summary>
        public const string SpanExecuteToolPrefix = "execute_tool ";

        /// <summary>Span name prefix for a pipeline or workflow stage ("stage:{name}").</summary>
        public const string SpanStagePrefix = "stage:";

        /// <summary>Span name prefix for a Tablix persistence operation ("tablix.persistence {kind}").</summary>
        public const string SpanPersistencePrefix = "tablix.persistence ";

        #endregion

        #region Attribute-Keys

        /// <summary>Outcome label: success, failure, error, rejected, or canceled.</summary>
        public const string AttrOutcome = "outcome";

        /// <summary>OpenTelemetry error type (exception type name or a bounded failure code).</summary>
        public const string AttrErrorType = "error.type";

        /// <summary>Pipeline or workflow stage name.</summary>
        public const string AttrStage = "stage";

        /// <summary>What started a crawl job (startup, api, api_stream, chat, query_retry, mcp, database_added, on_demand).</summary>
        public const string AttrTrigger = "trigger";

        /// <summary>OpenTelemetry database system name (sqlite, postgresql, mysql, microsoft.sql_server).</summary>
        public const string AttrDbSystem = "db.system.name";

        /// <summary>OpenTelemetry database operation name.</summary>
        public const string AttrDbOperation = "db.operation.name";

        /// <summary>OpenTelemetry database namespace (database name). Span attribute only.</summary>
        public const string AttrDbNamespace = "db.namespace";

        /// <summary>OpenTelemetry server address. Span attribute only.</summary>
        public const string AttrServerAddress = "server.address";

        /// <summary>OpenTelemetry server port. Span attribute only.</summary>
        public const string AttrServerPort = "server.port";

        /// <summary>OpenTelemetry generative AI provider name (openai, ollama, gemini).</summary>
        public const string AttrGenAiProvider = "gen_ai.provider.name";

        /// <summary>OpenTelemetry generative AI operation name.</summary>
        public const string AttrGenAiOperation = "gen_ai.operation.name";

        /// <summary>OpenTelemetry generative AI requested model. Span attribute only.</summary>
        public const string AttrGenAiRequestModel = "gen_ai.request.model";

        /// <summary>OpenTelemetry generative AI response model. Span attribute only.</summary>
        public const string AttrGenAiResponseModel = "gen_ai.response.model";

        /// <summary>OpenTelemetry generative AI token type (input or output).</summary>
        public const string AttrGenAiTokenType = "gen_ai.token.type";

        /// <summary>OpenTelemetry generative AI input token count. Span attribute only.</summary>
        public const string AttrGenAiInputTokens = "gen_ai.usage.input_tokens";

        /// <summary>OpenTelemetry generative AI output token count. Span attribute only.</summary>
        public const string AttrGenAiOutputTokens = "gen_ai.usage.output_tokens";

        /// <summary>OpenTelemetry tool name (chat tool or MCP tool).</summary>
        public const string AttrToolName = "gen_ai.tool.name";

        /// <summary>OpenTelemetry MCP method name. Span attribute only.</summary>
        public const string AttrMcpMethod = "mcp.method.name";

        /// <summary>Chat mode (sync or stream).</summary>
        public const string AttrChatMode = "mode";

        /// <summary>Chat execution path (bounded set chosen by the server, for example native_tool_calls).</summary>
        public const string AttrExecutionPath = "execution_path";

        /// <summary>Chat tool phase (native or fallback).</summary>
        public const string AttrToolPhase = "phase";

        /// <summary>Query source (rest, mcp, or chat).</summary>
        public const string AttrQuerySource = "source";

        /// <summary>SQL statement type (select, insert, update, delete, ..., other).</summary>
        public const string AttrStatement = "statement";

        /// <summary>Context scope (database or table).</summary>
        public const string AttrContextScope = "scope";

        /// <summary>Crawl cache lookup result (hit or miss).</summary>
        public const string AttrCacheResult = "result";

        /// <summary>Entry or provider state.</summary>
        public const string AttrState = "state";

        /// <summary>Component that recorded an error.</summary>
        public const string AttrComponent = "component";

        /// <summary>Build version label.</summary>
        public const string AttrVersion = "version";

        /// <summary>Runtime description label.</summary>
        public const string AttrRuntime = "runtime";

        /// <summary>Configuration flag name.</summary>
        public const string AttrFlag = "flag";

        /// <summary>Tablix database entry ID. Span attribute only (unbounded).</summary>
        public const string AttrDatabaseId = "tablix.database.id";

        /// <summary>Tablix model provider ID. Span attribute only (unbounded).</summary>
        public const string AttrProviderId = "tablix.provider.id";

        /// <summary>Tablix table ID. Span attribute only (unbounded).</summary>
        public const string AttrTableId = "tablix.table.id";

        /// <summary>Count of tables involved. Span attribute only.</summary>
        public const string AttrTableCount = "tablix.table.count";

        /// <summary>Count of rows returned. Span attribute only.</summary>
        public const string AttrRowCount = "tablix.row.count";

        /// <summary>Calling code function for a persistence operation. Span attribute only.</summary>
        public const string AttrCodeFunction = "code.function.name";

        #endregion

        #region Outcome-Values

        /// <summary>The operation completed successfully.</summary>
        public const string OutcomeSuccess = "success";

        /// <summary>The operation completed but reported a handled failure.</summary>
        public const string OutcomeFailure = "failure";

        /// <summary>The operation threw an exception.</summary>
        public const string OutcomeError = "error";

        /// <summary>The operation was refused by validation or policy before running.</summary>
        public const string OutcomeRejected = "rejected";

        /// <summary>The operation was canceled.</summary>
        public const string OutcomeCanceled = "canceled";

        #endregion

        #region Stage-Values

        /// <summary>Work waited for a concurrency slot or a batch position.</summary>
        public const string StageQueued = "queued";

        /// <summary>Crawl stage: connect and discover table names.</summary>
        public const string StageDiscover = "discover";

        /// <summary>Crawl stage: examine columns, keys, and indexes of each table.</summary>
        public const string StageExamine = "examine";

        /// <summary>Crawl stage: assign table identities and store the result in the crawl cache.</summary>
        public const string StageCache = "cache";

        /// <summary>Chat stage: load database, provider, and metadata, and build the prompt.</summary>
        public const string StagePrepare = "prepare";

        /// <summary>Chat stage: model call that may request native tools.</summary>
        public const string StageToolSelection = "tool_selection";

        /// <summary>Chat stage: fallback query planner model call.</summary>
        public const string StagePlanner = "planner";

        /// <summary>Chat stage: execution of a requested tool.</summary>
        public const string StageTool = "tool";

        /// <summary>Chat stage: final answer inference.</summary>
        public const string StageFinalInference = "final_inference";

        /// <summary>Context build stage: model inference.</summary>
        public const string StageInference = "inference";

        /// <summary>Context build stage: persist generated context.</summary>
        public const string StagePersist = "persist";

        #endregion

        #region Other-Values

        /// <summary>Crawl trigger: startup background crawl.</summary>
        public const string TriggerStartup = "startup";

        /// <summary>Crawl trigger: REST crawl endpoint.</summary>
        public const string TriggerApi = "api";

        /// <summary>Crawl trigger: REST streaming crawl endpoint.</summary>
        public const string TriggerApiStream = "api_stream";

        /// <summary>Crawl trigger: chat preparation found no metadata.</summary>
        public const string TriggerChat = "chat";

        /// <summary>Crawl trigger: schema refresh after a schema-related query failure.</summary>
        public const string TriggerQueryRetry = "query_retry";

        /// <summary>Crawl trigger: MCP tool needed metadata.</summary>
        public const string TriggerMcp = "mcp";

        /// <summary>Crawl trigger: background crawl after a database is added.</summary>
        public const string TriggerDatabaseAdded = "database_added";

        /// <summary>Crawl trigger: any other on-demand crawl.</summary>
        public const string TriggerOnDemand = "on_demand";

        /// <summary>Database operation: schema crawl.</summary>
        public const string DbOperationCrawl = "crawl";

        /// <summary>Database operation: SQL query.</summary>
        public const string DbOperationQuery = "query";

        /// <summary>Database operation: connectivity test.</summary>
        public const string DbOperationTestConnection = "test_connection";

        /// <summary>Persistence operation kind: read.</summary>
        public const string PersistenceRead = "read";

        /// <summary>Persistence operation kind: write transaction.</summary>
        public const string PersistenceWrite = "write";

        /// <summary>Query source: REST query endpoint.</summary>
        public const string SourceRest = "rest";

        /// <summary>Query or context source: MCP tool.</summary>
        public const string SourceMcp = "mcp";

        /// <summary>Query or context source: chat tool.</summary>
        public const string SourceChat = "chat";

        /// <summary>Context source: model-generated build.</summary>
        public const string SourceModel = "model";

        /// <summary>Context scope: database.</summary>
        public const string ScopeDatabase = "database";

        /// <summary>Context scope: table.</summary>
        public const string ScopeTable = "table";

        /// <summary>Chat mode: non-streaming.</summary>
        public const string ChatModeSync = "sync";

        /// <summary>Chat mode: server-sent events.</summary>
        public const string ChatModeStream = "stream";

        /// <summary>Model operation: chat completion.</summary>
        public const string ModelOperationChat = "chat";

        /// <summary>Model operation: streamed chat completion.</summary>
        public const string ModelOperationChatStream = "chat_stream";

        /// <summary>Model operation: chat completion with tools.</summary>
        public const string ModelOperationToolChat = "tool_chat";

        /// <summary>Model operation: streamed chat completion with tools.</summary>
        public const string ModelOperationToolChatStream = "tool_chat_stream";

        /// <summary>Model operation: background health check probe.</summary>
        public const string ModelOperationHealthCheck = "health_check";

        /// <summary>Model operation: operator-initiated connectivity test.</summary>
        public const string ModelOperationConnectivityTest = "connectivity_test";

        /// <summary>Token type: input (prompt) tokens.</summary>
        public const string TokenInput = "input";

        /// <summary>Token type: output (completion) tokens.</summary>
        public const string TokenOutput = "output";

        /// <summary>Cache result: hit.</summary>
        public const string CacheHit = "hit";

        /// <summary>Cache result: miss.</summary>
        public const string CacheMiss = "miss";

        /// <summary>State: crawled successfully.</summary>
        public const string StateCrawled = "crawled";

        /// <summary>State: degraded (crawl failed).</summary>
        public const string StateDegraded = "degraded";

        /// <summary>State: healthy.</summary>
        public const string StateHealthy = "healthy";

        /// <summary>State: unhealthy.</summary>
        public const string StateUnhealthy = "unhealthy";

        /// <summary>State: not monitored (disabled, or health checks off).</summary>
        public const string StateUnmonitored = "unmonitored";

        /// <summary>Error component: REST API.</summary>
        public const string ComponentRest = "rest";

        /// <summary>Error component: MCP server.</summary>
        public const string ComponentMcp = "mcp";

        /// <summary>Error component: chat workflow.</summary>
        public const string ComponentChat = "chat";

        /// <summary>Error component: crawl pipeline.</summary>
        public const string ComponentCrawl = "crawl";

        /// <summary>Error component: context builds.</summary>
        public const string ComponentContextBuild = "context_build";

        /// <summary>Error component: model provider health monitor.</summary>
        public const string ComponentHealthCheck = "health_check";

        /// <summary>Error component: persistence.</summary>
        public const string ComponentPersistence = "persistence";

        /// <summary>Error component: server lifecycle.</summary>
        public const string ComponentLifecycle = "lifecycle";

        /// <summary>Statement type used when a query cannot be classified.</summary>
        public const string StatementOther = "other";

        /// <summary>Unknown or unrecognized value for a bounded label.</summary>
        public const string ValueUnknown = "unknown";

        #endregion
    }
}
