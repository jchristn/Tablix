namespace Tablix.Core.Observability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using Tablix.Core.Enums;

    /// <summary>
    /// Tablix application metrics. Every instrument lives on the <see cref="TelemetryNames.MeterName"/> meter and is
    /// recorded through the typed methods below, which only accept bounded label values. Emission rides the .NET base
    /// class library: with no listener attached every call is a cheap no-op, and every recorder is best-effort, so a
    /// telemetry failure never propagates to the caller.
    /// Thread safety: all members are safe for concurrent use.
    /// </summary>
    public static class TablixMetrics
    {
        #region Public-Members

        /// <summary>
        /// The Tablix meter. Subscribe to <see cref="TelemetryNames.MeterName"/> to collect it.
        /// </summary>
        public static Meter Meter
        {
            get { return _Meter; }
        }

        /// <summary>
        /// Optional provider of crawl cache entry counts for the <see cref="TelemetryNames.MetricCrawlCacheEntries"/>
        /// gauge. Returns crawled and degraded counts. Null (the default) reports nothing.
        /// </summary>
        public static Func<CrawlCacheCounts> CrawlCacheCountsProvider { get; set; } = null;

        /// <summary>
        /// Optional provider of model provider health counts for the <see cref="TelemetryNames.MetricModelProviders"/>
        /// gauge. Null (the default) reports nothing.
        /// </summary>
        public static Func<ProviderHealthCounts> ProviderHealthCountsProvider { get; set; } = null;

        /// <summary>
        /// Optional provider of boolean configuration flags for the <see cref="TelemetryNames.MetricConfigFlag"/>
        /// gauge. Keys must be a small, fixed set of flag names; values are reported as 1 (true) or 0 (false).
        /// Null (the default) reports nothing.
        /// </summary>
        public static Func<IReadOnlyDictionary<string, bool>> ConfigFlagsProvider { get; set; } = null;

        /// <summary>
        /// Histogram bucket boundaries (seconds) for fast in-process operations, 100 microseconds to 2.5 seconds.
        /// Returns a copy.
        /// </summary>
        public static double[] FastBuckets
        {
            get { return (double[])_FastBuckets.Clone(); }
        }

        /// <summary>
        /// Histogram bucket boundaries (seconds) for typical request and database operations, 5 milliseconds to
        /// 30 seconds. Returns a copy.
        /// </summary>
        public static double[] DefaultBuckets
        {
            get { return (double[])_DefaultBuckets.Clone(); }
        }

        /// <summary>
        /// Histogram bucket boundaries (seconds) for slow pipeline and model operations, 50 milliseconds to
        /// 10 minutes. Returns a copy.
        /// </summary>
        public static double[] SlowBuckets
        {
            get { return (double[])_SlowBuckets.Clone(); }
        }

        /// <summary>
        /// Histogram bucket boundaries for row counts, 0 to 100000. Returns a copy.
        /// </summary>
        public static double[] RowBuckets
        {
            get { return (double[])_RowBuckets.Clone(); }
        }

        #endregion

        #region Private-Members

        private static readonly double[] _FastBuckets = { 0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5 };
        private static readonly double[] _DefaultBuckets = { 0.005, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1.0, 2.5, 5.0, 7.5, 10.0, 30.0 };
        private static readonly double[] _SlowBuckets = { 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 20.0, 30.0, 60.0, 120.0, 300.0, 600.0 };
        private static readonly double[] _RowBuckets = { 0, 1, 5, 10, 25, 50, 100, 250, 500, 1000, 5000, 10000, 50000, 100000 };

        private static readonly Meter _Meter = new Meter(TelemetryNames.MeterName, Helpers.Constants.ProductVersion);
        private static readonly ConcurrentDictionary<string, long> _LastCrawlSuccessUnix = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
        private static long _LastHealthCycleUnix = 0;

        private static readonly Counter<long> _CrawlJobs = _Meter.CreateCounter<long>(TelemetryNames.MetricCrawlJobs, "{job}", "Schema crawl jobs by trigger, database system, and outcome.");
        private static readonly Histogram<double> _CrawlDuration = CreateHistogram(TelemetryNames.MetricCrawlDuration, "s", "End-to-end schema crawl job duration.", _SlowBuckets);
        private static readonly Histogram<double> _CrawlStageDuration = CreateHistogram(TelemetryNames.MetricCrawlStageDuration, "s", "Duration of each crawl pipeline stage, including queued.", _SlowBuckets);
        private static readonly Counter<long> _CrawlStageEvents = _Meter.CreateCounter<long>(TelemetryNames.MetricCrawlStageEvents, "{event}", "Crawl pipeline stage completions by stage and outcome.");
        private static readonly Counter<long> _CrawlTables = _Meter.CreateCounter<long>(TelemetryNames.MetricCrawlTables, "{table}", "Tables discovered by successful crawls.");
        private static readonly UpDownCounter<long> _CrawlActive = _Meter.CreateUpDownCounter<long>(TelemetryNames.MetricCrawlActive, "{job}", "Crawl jobs currently running.");
        private static readonly Counter<long> _CrawlCacheLookups = _Meter.CreateCounter<long>(TelemetryNames.MetricCrawlCacheLookups, "{lookup}", "Crawl cache lookups by result.");

        private static readonly Counter<long> _DbClientOperations = _Meter.CreateCounter<long>(TelemetryNames.MetricDbClientOperations, "{operation}", "Operations against user-configured databases.");
        private static readonly Histogram<double> _DbClientDuration = CreateHistogram(TelemetryNames.MetricDbClientDuration, "s", "Duration of operations against user-configured databases.", _DefaultBuckets);
        private static readonly UpDownCounter<long> _DbClientActive = _Meter.CreateUpDownCounter<long>(TelemetryNames.MetricDbClientActive, "{operation}", "Operations against user-configured databases in flight.");

        private static readonly Counter<long> _QueryExecutions = _Meter.CreateCounter<long>(TelemetryNames.MetricQueryExecutions, "{query}", "SQL query executions by source, statement type, and outcome.");
        private static readonly Histogram<double> _QueryDuration = CreateHistogram(TelemetryNames.MetricQueryDuration, "s", "SQL query execution duration including validation.", _DefaultBuckets);
        private static readonly Histogram<long> _QueryRows = CreateLongHistogram(TelemetryNames.MetricQueryRows, "{row}", "Rows returned by successful queries.", _RowBuckets);
        private static readonly Counter<long> _QuerySchemaRefreshes = _Meter.CreateCounter<long>(TelemetryNames.MetricQuerySchemaRefreshes, "{refresh}", "Schema refreshes triggered by schema-related query failures.");

        private static readonly Counter<long> _ChatRequests = _Meter.CreateCounter<long>(TelemetryNames.MetricChatRequests, "{request}", "Chat requests by mode, execution path, and outcome.");
        private static readonly Histogram<double> _ChatDuration = CreateHistogram(TelemetryNames.MetricChatDuration, "s", "End-to-end chat request duration.", _SlowBuckets);
        private static readonly Histogram<double> _ChatStageDuration = CreateHistogram(TelemetryNames.MetricChatStageDuration, "s", "Duration of each chat workflow stage.", _SlowBuckets);
        private static readonly Counter<long> _ChatToolCalls = _Meter.CreateCounter<long>(TelemetryNames.MetricChatToolCalls, "{call}", "Chat tool calls by tool, phase, and outcome.");
        private static readonly Histogram<double> _ChatToolCallDuration = CreateHistogram(TelemetryNames.MetricChatToolCallDuration, "s", "Chat tool call duration.", _DefaultBuckets);
        private static readonly Histogram<double> _ChatTimeToFirstToken = CreateHistogram(TelemetryNames.MetricChatTimeToFirstToken, "s", "Time to first streamed token from a model provider.", _SlowBuckets);

        private static readonly Counter<long> _ContextBuilds = _Meter.CreateCounter<long>(TelemetryNames.MetricContextBuilds, "{build}", "Model-generated context builds by scope and outcome.");
        private static readonly Histogram<double> _ContextBuildDuration = CreateHistogram(TelemetryNames.MetricContextBuildDuration, "s", "Model-generated context build duration.", _SlowBuckets);
        private static readonly Histogram<double> _ContextBuildStageDuration = CreateHistogram(TelemetryNames.MetricContextBuildStageDuration, "s", "Duration of each context build stage.", _SlowBuckets);
        private static readonly UpDownCounter<long> _ContextBuildSlotsInUse = _Meter.CreateUpDownCounter<long>(TelemetryNames.MetricContextBuildSlotsInUse, "{slot}", "Context build concurrency slots currently held.");
        private static readonly Counter<long> _ContextUpdates = _Meter.CreateCounter<long>(TelemetryNames.MetricContextUpdates, "{update}", "Context writes by scope, source, and outcome.");

        private static readonly Counter<long> _ModelRequests = _Meter.CreateCounter<long>(TelemetryNames.MetricModelRequests, "{request}", "Model provider requests by provider, operation, and outcome.");
        private static readonly Histogram<double> _ModelRequestDuration = CreateHistogram(TelemetryNames.MetricModelRequestDuration, "s", "Model provider request duration.", _SlowBuckets);
        private static readonly Counter<long> _ModelTokens = _Meter.CreateCounter<long>(TelemetryNames.MetricModelTokens, "{token}", "Model tokens by provider and token type.");
        private static readonly Counter<long> _ModelHealthTransitions = _Meter.CreateCounter<long>(TelemetryNames.MetricModelHealthTransitions, "{transition}", "Model provider health state transitions.");

        private static readonly Counter<long> _McpToolCalls = _Meter.CreateCounter<long>(TelemetryNames.MetricMcpToolCalls, "{call}", "MCP tool calls by tool and outcome.");
        private static readonly Histogram<double> _McpToolDuration = CreateHistogram(TelemetryNames.MetricMcpToolDuration, "s", "MCP tool call duration.", _DefaultBuckets);
        private static readonly UpDownCounter<long> _McpToolActive = _Meter.CreateUpDownCounter<long>(TelemetryNames.MetricMcpToolActive, "{call}", "MCP tool calls in flight.");

        private static readonly Counter<long> _PersistenceOperations = _Meter.CreateCounter<long>(TelemetryNames.MetricPersistenceOperations, "{operation}", "Tablix persistence operations by kind and outcome.");
        private static readonly Histogram<double> _PersistenceDuration = CreateHistogram(TelemetryNames.MetricPersistenceDuration, "s", "Persistence operation duration, excluding the lock wait.", _FastBuckets);
        private static readonly Histogram<double> _PersistenceLockWait = CreateHistogram(TelemetryNames.MetricPersistenceLockWait, "s", "Time spent waiting for the persistence operation slot.", _FastBuckets);
        private static readonly UpDownCounter<long> _PersistenceLockWaiting = _Meter.CreateUpDownCounter<long>(TelemetryNames.MetricPersistenceLockWaiting, "{operation}", "Persistence operations waiting for the operation slot.");

        private static readonly Counter<long> _Errors = _Meter.CreateCounter<long>(TelemetryNames.MetricErrors, "{error}", "Handled errors by component and error type.");
        private static readonly Counter<long> _SettingsUpdates = _Meter.CreateCounter<long>(TelemetryNames.MetricSettingsUpdates, "{update}", "Settings updates by outcome.");

        #endregion

        #region Constructors-and-Factories

        static TablixMetrics()
        {
            _Meter.CreateObservableGauge<long>(TelemetryNames.MetricCrawlLastSuccess, ObserveLastCrawlSuccess, "s", "Unix time of the last successful crawl, by database system.");
            _Meter.CreateObservableGauge<long>(TelemetryNames.MetricCrawlCacheEntries, ObserveCrawlCacheEntries, "{entry}", "Crawl cache entries by state.");
            _Meter.CreateObservableGauge<long>(TelemetryNames.MetricModelHealthLastCycle, ObserveHealthLastCycle, "s", "Unix time the health monitor last completed a scheduling cycle.");
            _Meter.CreateObservableGauge<long>(TelemetryNames.MetricModelProviders, ObserveModelProviders, "{provider}", "Configured model providers by health state.");
            _Meter.CreateObservableGauge<long>(TelemetryNames.MetricBuildInfo, ObserveBuildInfo, "{info}", "Build information (always 1).");
            _Meter.CreateObservableGauge<long>(TelemetryNames.MetricConfigFlag, ObserveConfigFlags, "{flag}", "Safe boolean configuration flags (1 enabled, 0 disabled).");
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Map a Tablix database type to the OpenTelemetry db.system.name value.
        /// </summary>
        /// <param name="type">Database type.</param>
        /// <returns>sqlite, postgresql, mysql, microsoft.sql_server, or unknown.</returns>
        public static string DbSystem(DatabaseTypeEnum type)
        {
            switch (type)
            {
                case DatabaseTypeEnum.Sqlite: return "sqlite";
                case DatabaseTypeEnum.Postgresql: return "postgresql";
                case DatabaseTypeEnum.Mysql: return "mysql";
                case DatabaseTypeEnum.SqlServer: return "microsoft.sql_server";
                default: return TelemetryNames.ValueUnknown;
            }
        }

        /// <summary>
        /// Map a model provider type to the OpenTelemetry gen_ai.provider.name value.
        /// </summary>
        /// <param name="type">Provider type.</param>
        /// <returns>openai, openai_compatible, gemini, ollama, or unknown.</returns>
        public static string GenAiProvider(ModelProviderTypeEnum type)
        {
            switch (type)
            {
                case ModelProviderTypeEnum.OpenAI: return "openai";
                case ModelProviderTypeEnum.OpenAICompatible: return "openai_compatible";
                case ModelProviderTypeEnum.Gemini: return "gemini";
                case ModelProviderTypeEnum.Ollama: return "ollama";
                default: return TelemetryNames.ValueUnknown;
            }
        }

        /// <summary>
        /// Normalize a context write source to a bounded label value.
        /// </summary>
        /// <param name="source">Source recorded with the context write (user, mcp, chat, model). May be null.</param>
        /// <returns>user, mcp, chat, model, or other.</returns>
        public static string ContextSource(string source)
        {
            if (String.IsNullOrWhiteSpace(source)) return "user";

            string normalized = source.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "user":
                case "mcp":
                case "chat":
                case "model":
                    return normalized;
                default:
                    return "other";
            }
        }

        /// <summary>
        /// Map an exception to a bounded outcome value: canceled for cancellation, otherwise error.
        /// </summary>
        /// <param name="ex">Exception. May be null.</param>
        /// <returns>Outcome value.</returns>
        public static string OutcomeOf(Exception ex)
        {
            return ex is OperationCanceledException ? TelemetryNames.OutcomeCanceled : TelemetryNames.OutcomeError;
        }

        /// <summary>
        /// Elapsed seconds since a <see cref="Stopwatch.GetTimestamp"/> value.
        /// </summary>
        /// <param name="startTimestamp">Start timestamp.</param>
        /// <returns>Elapsed seconds.</returns>
        public static double SecondsSince(long startTimestamp)
        {
            return Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
        }

        /// <summary>
        /// Record a completed crawl job.
        /// </summary>
        /// <param name="trigger">Trigger value from <see cref="TelemetryNames"/>.</param>
        /// <param name="dbSystem">Database system name.</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        /// <param name="tableCount">Tables discovered (successful crawls only).</param>
        public static void RecordCrawlJob(string trigger, string dbSystem, string outcome, double seconds, int tableCount)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrTrigger, trigger ?? TelemetryNames.TriggerOnDemand },
                    { TelemetryNames.AttrDbSystem, dbSystem ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _CrawlJobs.Add(1, tags);
                _CrawlDuration.Record(seconds, tags);

                if (outcome == TelemetryNames.OutcomeSuccess)
                {
                    _CrawlTables.Add(Math.Max(0, tableCount), new KeyValuePair<string, object>(TelemetryNames.AttrDbSystem, dbSystem ?? TelemetryNames.ValueUnknown));
                    _LastCrawlSuccessUnix[dbSystem ?? TelemetryNames.ValueUnknown] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record one completed crawl pipeline stage.
        /// </summary>
        /// <param name="stage">Stage value from <see cref="TelemetryNames"/>.</param>
        /// <param name="dbSystem">Database system name.</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordCrawlStage(string stage, string dbSystem, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrStage, stage },
                    { TelemetryNames.AttrDbSystem, dbSystem ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _CrawlStageDuration.Record(seconds, tags);
                _CrawlStageEvents.Add(1, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Adjust the running crawl job count.
        /// </summary>
        /// <param name="delta">+1 when a job starts, -1 when it ends.</param>
        public static void AddCrawlActive(int delta)
        {
            try { _CrawlActive.Add(delta); } catch (Exception) { }
        }

        /// <summary>
        /// Record a crawl cache lookup.
        /// </summary>
        /// <param name="hit">True for a hit, false for a miss.</param>
        public static void RecordCrawlCacheLookup(bool hit)
        {
            try
            {
                _CrawlCacheLookups.Add(1, new KeyValuePair<string, object>(TelemetryNames.AttrCacheResult, hit ? TelemetryNames.CacheHit : TelemetryNames.CacheMiss));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record an operation against a user-configured database.
        /// </summary>
        /// <param name="dbSystem">Database system name.</param>
        /// <param name="operation">Operation value (crawl, query, test_connection).</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordDbClientOperation(string dbSystem, string operation, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrDbSystem, dbSystem ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrDbOperation, operation },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _DbClientOperations.Add(1, tags);
                _DbClientDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Adjust the in-flight count of operations against user-configured databases.
        /// </summary>
        /// <param name="dbSystem">Database system name.</param>
        /// <param name="delta">+1 when an operation starts, -1 when it ends.</param>
        public static void AddDbClientActive(string dbSystem, int delta)
        {
            try { _DbClientActive.Add(delta, new KeyValuePair<string, object>(TelemetryNames.AttrDbSystem, dbSystem ?? TelemetryNames.ValueUnknown)); } catch (Exception) { }
        }

        /// <summary>
        /// Record a SQL query execution, including rejected ones.
        /// </summary>
        /// <param name="source">Source value (rest, mcp, chat).</param>
        /// <param name="statement">Statement type from <see cref="Helpers.QueryValidator.GetStatementType"/>.</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        /// <param name="rows">Rows returned; recorded only for successful queries.</param>
        public static void RecordQuery(string source, string statement, string outcome, double seconds, int rows)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrQuerySource, source },
                    { TelemetryNames.AttrStatement, statement ?? TelemetryNames.StatementOther },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _QueryExecutions.Add(1, tags);
                _QueryDuration.Record(seconds, tags);

                if (outcome == TelemetryNames.OutcomeSuccess)
                {
                    TagList rowTags = new TagList
                    {
                        { TelemetryNames.AttrQuerySource, source },
                        { TelemetryNames.AttrStatement, statement ?? TelemetryNames.StatementOther }
                    };
                    _QueryRows.Record(Math.Max(0, rows), rowTags);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a schema refresh triggered by a schema-related query failure.
        /// </summary>
        /// <param name="outcome">Outcome of the retried query.</param>
        public static void RecordSchemaRefresh(string outcome)
        {
            try { _QuerySchemaRefreshes.Add(1, new KeyValuePair<string, object>(TelemetryNames.AttrOutcome, outcome)); } catch (Exception) { }
        }

        /// <summary>
        /// Record a completed chat request.
        /// </summary>
        /// <param name="mode">Chat mode (sync or stream).</param>
        /// <param name="executionPath">Execution path chosen by the server (bounded set), or unknown.</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordChat(string mode, string executionPath, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrChatMode, mode },
                    { TelemetryNames.AttrExecutionPath, executionPath ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ChatRequests.Add(1, tags);

                TagList durationTags = new TagList
                {
                    { TelemetryNames.AttrChatMode, mode },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ChatDuration.Record(seconds, durationTags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record one chat workflow stage.
        /// </summary>
        /// <param name="stage">Stage value.</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordChatStage(string stage, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrStage, stage },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ChatStageDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record one chat tool call.
        /// </summary>
        /// <param name="toolName">Tool name; must be one of the registered Tablix chat tools or unknown.</param>
        /// <param name="phase">Phase (native or fallback).</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordChatToolCall(string toolName, string phase, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrToolName, toolName ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrToolPhase, phase ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ChatToolCalls.Add(1, tags);

                TagList durationTags = new TagList
                {
                    { TelemetryNames.AttrToolName, toolName ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ChatToolCallDuration.Record(seconds, durationTags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record the time to the first streamed token.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="seconds">Seconds to first token.</param>
        public static void RecordTimeToFirstToken(string provider, double seconds)
        {
            try
            {
                if (seconds <= 0) return;
                _ChatTimeToFirstToken.Record(seconds, new KeyValuePair<string, object>(TelemetryNames.AttrGenAiProvider, provider ?? TelemetryNames.ValueUnknown));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a completed context build.
        /// </summary>
        /// <param name="scope">Scope (database or table).</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordContextBuild(string scope, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrContextScope, scope },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ContextBuilds.Add(1, tags);
                _ContextBuildDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record one context build stage.
        /// </summary>
        /// <param name="scope">Scope (database or table).</param>
        /// <param name="stage">Stage (queued, inference, persist).</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordContextBuildStage(string scope, string stage, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrContextScope, scope },
                    { TelemetryNames.AttrStage, stage },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ContextBuildStageDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Adjust the count of held context build concurrency slots.
        /// </summary>
        /// <param name="delta">+1 when a slot is acquired, -1 when released.</param>
        public static void AddContextBuildSlotsInUse(int delta)
        {
            try { _ContextBuildSlotsInUse.Add(delta); } catch (Exception) { }
        }

        /// <summary>
        /// Record a context write.
        /// </summary>
        /// <param name="scope">Scope (database or table).</param>
        /// <param name="source">Source (rest, mcp, chat, model).</param>
        /// <param name="outcome">Outcome value.</param>
        public static void RecordContextUpdate(string scope, string source, string outcome)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrContextScope, scope },
                    { TelemetryNames.AttrQuerySource, source },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ContextUpdates.Add(1, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a model provider request.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="operation">Operation value.</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordModelRequest(string provider, string operation, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrGenAiProvider, provider ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrGenAiOperation, operation },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _ModelRequests.Add(1, tags);
                _ModelRequestDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record model token usage.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="inputTokens">Input tokens; ignored when not positive.</param>
        /// <param name="outputTokens">Output tokens; ignored when not positive.</param>
        public static void RecordModelTokens(string provider, long inputTokens, long outputTokens)
        {
            try
            {
                string providerName = provider ?? TelemetryNames.ValueUnknown;
                if (inputTokens > 0)
                {
                    _ModelTokens.Add(inputTokens, new TagList
                    {
                        { TelemetryNames.AttrGenAiProvider, providerName },
                        { TelemetryNames.AttrGenAiTokenType, TelemetryNames.TokenInput }
                    });
                }

                if (outputTokens > 0)
                {
                    _ModelTokens.Add(outputTokens, new TagList
                    {
                        { TelemetryNames.AttrGenAiProvider, providerName },
                        { TelemetryNames.AttrGenAiTokenType, TelemetryNames.TokenOutput }
                    });
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a model provider health state transition.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="healthy">New state.</param>
        public static void RecordHealthTransition(string provider, bool healthy)
        {
            try
            {
                _ModelHealthTransitions.Add(1, new TagList
                {
                    { TelemetryNames.AttrGenAiProvider, provider ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrState, healthy ? TelemetryNames.StateHealthy : TelemetryNames.StateUnhealthy }
                });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Mark the completion of one health monitor scheduling cycle (the background job heartbeat).
        /// </summary>
        public static void MarkHealthCycle()
        {
            Interlocked.Exchange(ref _LastHealthCycleUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        /// <summary>
        /// Record an MCP tool call.
        /// </summary>
        /// <param name="toolName">Tool name (registered Tablix MCP tool).</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds.</param>
        public static void RecordMcpToolCall(string toolName, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrToolName, toolName ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _McpToolCalls.Add(1, tags);
                _McpToolDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Adjust the in-flight MCP tool call count.
        /// </summary>
        /// <param name="delta">+1 when a call starts, -1 when it ends.</param>
        public static void AddMcpToolActive(int delta)
        {
            try { _McpToolActive.Add(delta); } catch (Exception) { }
        }

        /// <summary>
        /// Record a persistence operation.
        /// </summary>
        /// <param name="kind">Kind (read or write).</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="seconds">Duration in seconds, excluding the lock wait.</param>
        public static void RecordPersistenceOperation(string kind, string outcome, double seconds)
        {
            try
            {
                TagList tags = new TagList
                {
                    { TelemetryNames.AttrDbOperation, kind },
                    { TelemetryNames.AttrOutcome, outcome }
                };
                _PersistenceOperations.Add(1, tags);
                _PersistenceDuration.Record(seconds, tags);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record the wait for the persistence operation slot.
        /// </summary>
        /// <param name="kind">Kind (read or write).</param>
        /// <param name="seconds">Wait in seconds.</param>
        public static void RecordPersistenceLockWait(string kind, double seconds)
        {
            try { _PersistenceLockWait.Record(seconds, new KeyValuePair<string, object>(TelemetryNames.AttrDbOperation, kind)); } catch (Exception) { }
        }

        /// <summary>
        /// Adjust the count of persistence operations waiting for the operation slot.
        /// </summary>
        /// <param name="delta">+1 when an operation starts waiting, -1 when it stops waiting.</param>
        public static void AddPersistenceLockWaiting(int delta)
        {
            try { _PersistenceLockWaiting.Add(delta); } catch (Exception) { }
        }

        /// <summary>
        /// Record a handled error.
        /// </summary>
        /// <param name="component">Component value from <see cref="TelemetryNames"/>.</param>
        /// <param name="ex">Exception; its type name is the error.type label. May be null.</param>
        public static void RecordError(string component, Exception ex)
        {
            RecordError(component, ex == null ? TelemetryNames.ValueUnknown : ex.GetType().Name);
        }

        /// <summary>
        /// Record a handled error with an explicit bounded error type.
        /// </summary>
        /// <param name="component">Component value from <see cref="TelemetryNames"/>.</param>
        /// <param name="errorType">Bounded error type (exception type name or a fixed failure code).</param>
        public static void RecordError(string component, string errorType)
        {
            try
            {
                _Errors.Add(1, new TagList
                {
                    { TelemetryNames.AttrComponent, component ?? TelemetryNames.ValueUnknown },
                    { TelemetryNames.AttrErrorType, errorType ?? TelemetryNames.ValueUnknown }
                });
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record a settings update.
        /// </summary>
        /// <param name="outcome">Outcome value.</param>
        public static void RecordSettingsUpdate(string outcome)
        {
            try { _SettingsUpdates.Add(1, new KeyValuePair<string, object>(TelemetryNames.AttrOutcome, outcome)); } catch (Exception) { }
        }

        #endregion

        #region Private-Methods

        private static Histogram<double> CreateHistogram(string name, string unit, string description, double[] buckets)
        {
            return _Meter.CreateHistogram<double>(name, unit, description, null, new InstrumentAdvice<double> { HistogramBucketBoundaries = buckets });
        }

        private static Histogram<long> CreateLongHistogram(string name, string unit, string description, double[] buckets)
        {
            long[] boundaries = new long[buckets.Length];
            for (int i = 0; i < buckets.Length; i++) boundaries[i] = (long)buckets[i];
            return _Meter.CreateHistogram<long>(name, unit, description, null, new InstrumentAdvice<long> { HistogramBucketBoundaries = boundaries });
        }

        private static IEnumerable<Measurement<long>> ObserveLastCrawlSuccess()
        {
            List<Measurement<long>> measurements = new List<Measurement<long>>();
            foreach (KeyValuePair<string, long> entry in _LastCrawlSuccessUnix)
            {
                measurements.Add(new Measurement<long>(entry.Value, new KeyValuePair<string, object>(TelemetryNames.AttrDbSystem, entry.Key)));
            }

            return measurements;
        }

        private static IEnumerable<Measurement<long>> ObserveCrawlCacheEntries()
        {
            List<Measurement<long>> measurements = new List<Measurement<long>>();
            try
            {
                Func<CrawlCacheCounts> provider = CrawlCacheCountsProvider;
                if (provider == null) return measurements;

                CrawlCacheCounts counts = provider();
                if (counts == null) return measurements;

                measurements.Add(new Measurement<long>(counts.Crawled, new KeyValuePair<string, object>(TelemetryNames.AttrState, TelemetryNames.StateCrawled)));
                measurements.Add(new Measurement<long>(counts.Degraded, new KeyValuePair<string, object>(TelemetryNames.AttrState, TelemetryNames.StateDegraded)));
            }
            catch (Exception)
            {
            }

            return measurements;
        }

        private static IEnumerable<Measurement<long>> ObserveHealthLastCycle()
        {
            long value = Interlocked.Read(ref _LastHealthCycleUnix);
            if (value <= 0) return Array.Empty<Measurement<long>>();
            return new[] { new Measurement<long>(value) };
        }

        private static IEnumerable<Measurement<long>> ObserveModelProviders()
        {
            List<Measurement<long>> measurements = new List<Measurement<long>>();
            try
            {
                Func<ProviderHealthCounts> provider = ProviderHealthCountsProvider;
                if (provider == null) return measurements;

                ProviderHealthCounts counts = provider();
                if (counts == null) return measurements;

                measurements.Add(new Measurement<long>(counts.Healthy, new KeyValuePair<string, object>(TelemetryNames.AttrState, TelemetryNames.StateHealthy)));
                measurements.Add(new Measurement<long>(counts.Unhealthy, new KeyValuePair<string, object>(TelemetryNames.AttrState, TelemetryNames.StateUnhealthy)));
                measurements.Add(new Measurement<long>(counts.Unmonitored, new KeyValuePair<string, object>(TelemetryNames.AttrState, TelemetryNames.StateUnmonitored)));
            }
            catch (Exception)
            {
            }

            return measurements;
        }

        private static IEnumerable<Measurement<long>> ObserveBuildInfo()
        {
            return new[]
            {
                new Measurement<long>(1,
                    new KeyValuePair<string, object>(TelemetryNames.AttrVersion, Helpers.Constants.ProductVersion),
                    new KeyValuePair<string, object>(TelemetryNames.AttrRuntime, RuntimeInformation.FrameworkDescription))
            };
        }

        private static IEnumerable<Measurement<long>> ObserveConfigFlags()
        {
            List<Measurement<long>> measurements = new List<Measurement<long>>();
            try
            {
                Func<IReadOnlyDictionary<string, bool>> provider = ConfigFlagsProvider;
                if (provider == null) return measurements;

                IReadOnlyDictionary<string, bool> flags = provider();
                if (flags == null) return measurements;

                foreach (KeyValuePair<string, bool> flag in flags)
                {
                    measurements.Add(new Measurement<long>(flag.Value ? 1 : 0, new KeyValuePair<string, object>(TelemetryNames.AttrFlag, flag.Key)));
                }
            }
            catch (Exception)
            {
            }

            return measurements;
        }

        #endregion
    }
}
