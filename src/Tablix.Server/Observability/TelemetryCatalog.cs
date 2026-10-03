namespace Tablix.Server.Observability
{
    using System.Collections.Generic;
    using Radiant;
    using Tablix.Core.Observability;

    /// <summary>
    /// The Tablix metric catalog as Radiant conventions: every instrument on the <see cref="TelemetryNames.MeterName"/>
    /// meter with its kind, unit, histogram buckets, and the complete set of label keys it may carry. Registered with
    /// <c>settings.Metrics.DefineAll</c> so histogram buckets are applied as views and the declared label sets are
    /// documented in one place. Tests assert this catalog matches the instruments actually published.
    /// Thread safety: immutable after construction; safe for concurrent use.
    /// </summary>
    public static class TelemetryCatalog
    {
        #region Public-Members

        /// <summary>
        /// Every Tablix metric convention.
        /// </summary>
        public static IReadOnlyList<Convention> All
        {
            get { return _All; }
        }

        #endregion

        #region Private-Members

        private static readonly List<Convention> _All = Build();

        #endregion

        #region Private-Methods

        private static List<Convention> Build()
        {
            double[] fast = TablixMetrics.FastBuckets;
            double[] standard = TablixMetrics.DefaultBuckets;
            double[] slow = TablixMetrics.SlowBuckets;
            double[] rows = TablixMetrics.RowBuckets;

            return new List<Convention>
            {
                Convention.Counter(TelemetryNames.MetricCrawlJobs, "{job}", TelemetryNames.AttrTrigger, TelemetryNames.AttrDbSystem, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricCrawlDuration, "s", slow, TelemetryNames.AttrTrigger, TelemetryNames.AttrDbSystem, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricCrawlStageDuration, "s", slow, TelemetryNames.AttrStage, TelemetryNames.AttrDbSystem, TelemetryNames.AttrOutcome),
                Convention.Counter(TelemetryNames.MetricCrawlStageEvents, "{event}", TelemetryNames.AttrStage, TelemetryNames.AttrDbSystem, TelemetryNames.AttrOutcome),
                Convention.Gauge(TelemetryNames.MetricCrawlLastSuccess, "s", TelemetryNames.AttrDbSystem),
                Convention.Counter(TelemetryNames.MetricCrawlTables, "{table}", TelemetryNames.AttrDbSystem),
                Convention.UpDownCounter(TelemetryNames.MetricCrawlActive, "{job}"),
                Convention.Counter(TelemetryNames.MetricCrawlCacheLookups, "{lookup}", TelemetryNames.AttrCacheResult),
                Convention.Gauge(TelemetryNames.MetricCrawlCacheEntries, "{entry}", TelemetryNames.AttrState),

                Convention.Counter(TelemetryNames.MetricDbClientOperations, "{operation}", TelemetryNames.AttrDbSystem, TelemetryNames.AttrDbOperation, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricDbClientDuration, "s", standard, TelemetryNames.AttrDbSystem, TelemetryNames.AttrDbOperation, TelemetryNames.AttrOutcome),
                Convention.UpDownCounter(TelemetryNames.MetricDbClientActive, "{operation}", TelemetryNames.AttrDbSystem),

                Convention.Counter(TelemetryNames.MetricQueryExecutions, "{query}", TelemetryNames.AttrQuerySource, TelemetryNames.AttrStatement, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricQueryDuration, "s", standard, TelemetryNames.AttrQuerySource, TelemetryNames.AttrStatement, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricQueryRows, "{row}", rows, TelemetryNames.AttrQuerySource, TelemetryNames.AttrStatement),
                Convention.Counter(TelemetryNames.MetricQuerySchemaRefreshes, "{refresh}", TelemetryNames.AttrOutcome),

                Convention.Counter(TelemetryNames.MetricChatRequests, "{request}", TelemetryNames.AttrChatMode, TelemetryNames.AttrExecutionPath, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricChatDuration, "s", slow, TelemetryNames.AttrChatMode, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricChatStageDuration, "s", slow, TelemetryNames.AttrStage, TelemetryNames.AttrOutcome),
                Convention.Counter(TelemetryNames.MetricChatToolCalls, "{call}", TelemetryNames.AttrToolName, TelemetryNames.AttrToolPhase, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricChatToolCallDuration, "s", standard, TelemetryNames.AttrToolName, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricChatTimeToFirstToken, "s", slow, TelemetryNames.AttrGenAiProvider),

                Convention.Counter(TelemetryNames.MetricContextBuilds, "{build}", TelemetryNames.AttrContextScope, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricContextBuildDuration, "s", slow, TelemetryNames.AttrContextScope, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricContextBuildStageDuration, "s", slow, TelemetryNames.AttrContextScope, TelemetryNames.AttrStage, TelemetryNames.AttrOutcome),
                Convention.UpDownCounter(TelemetryNames.MetricContextBuildSlotsInUse, "{slot}"),
                Convention.Counter(TelemetryNames.MetricContextUpdates, "{update}", TelemetryNames.AttrContextScope, TelemetryNames.AttrQuerySource, TelemetryNames.AttrOutcome),

                Convention.Counter(TelemetryNames.MetricModelRequests, "{request}", TelemetryNames.AttrGenAiProvider, TelemetryNames.AttrGenAiOperation, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricModelRequestDuration, "s", slow, TelemetryNames.AttrGenAiProvider, TelemetryNames.AttrGenAiOperation, TelemetryNames.AttrOutcome),
                Convention.Counter(TelemetryNames.MetricModelTokens, "{token}", TelemetryNames.AttrGenAiProvider, TelemetryNames.AttrGenAiTokenType),
                Convention.Counter(TelemetryNames.MetricModelHealthTransitions, "{transition}", TelemetryNames.AttrGenAiProvider, TelemetryNames.AttrState),
                Convention.Gauge(TelemetryNames.MetricModelHealthLastCycle, "s"),
                Convention.Gauge(TelemetryNames.MetricModelProviders, "{provider}", TelemetryNames.AttrState),

                Convention.Counter(TelemetryNames.MetricMcpToolCalls, "{call}", TelemetryNames.AttrToolName, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricMcpToolDuration, "s", standard, TelemetryNames.AttrToolName, TelemetryNames.AttrOutcome),
                Convention.UpDownCounter(TelemetryNames.MetricMcpToolActive, "{call}"),

                Convention.Counter(TelemetryNames.MetricPersistenceOperations, "{operation}", TelemetryNames.AttrDbOperation, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricPersistenceDuration, "s", fast, TelemetryNames.AttrDbOperation, TelemetryNames.AttrOutcome),
                Convention.Histogram(TelemetryNames.MetricPersistenceLockWait, "s", fast, TelemetryNames.AttrDbOperation),
                Convention.UpDownCounter(TelemetryNames.MetricPersistenceLockWaiting, "{operation}"),

                Convention.Counter(TelemetryNames.MetricErrors, "{error}", TelemetryNames.AttrComponent, TelemetryNames.AttrErrorType),
                Convention.Counter(TelemetryNames.MetricSettingsUpdates, "{update}", TelemetryNames.AttrOutcome),
                Convention.Gauge(TelemetryNames.MetricBuildInfo, "{info}", TelemetryNames.AttrVersion, TelemetryNames.AttrRuntime),
                Convention.Gauge(TelemetryNames.MetricConfigFlag, "{flag}", TelemetryNames.AttrFlag)
            };
        }

        #endregion
    }
}
