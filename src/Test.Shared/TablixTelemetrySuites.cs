namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using PolyPrompt.Models;
    using Radiant;
    using SyslogLogging;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Observability;
    using Tablix.Core.Persistence;
    using Tablix.Core.Persistence.Sqlite;
    using Tablix.Core.Settings;
    using Tablix.Server;
    using Tablix.Server.Observability;
    using Tablix.Server.Services;
    using Touchstone.Core;
    using Voltaic.Core;

    /// <summary>
    /// Touchstone suites proving Tablix emits its documented telemetry: metrics and spans for every inventory
    /// category (crawl pipeline, database client, query, persistence, context, model provider, health monitor, MCP,
    /// chat, lifecycle), the failure paths, the no-listener path, the metric catalog, and an end-to-end server run
    /// scraped through the Prometheus endpoint.
    /// </summary>
    public static class TablixTelemetrySuites
    {
        #region Private-Members

        private const string _SampleTraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
        private const string _SampleSpanId = "00f067aa0ba902b7";
        private const string _SampleTraceParent = "00-" + _SampleTraceId + "-" + _SampleSpanId + "-01";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Telemetry suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TelemetrySuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Telemetry",
                displayName: "Telemetry",
                cases: new List<TestCaseDescriptor>
                {
                    Case("NoListenerIsSafe", "Recording without any listener never throws", NoListenerIsSafeAsync),
                    Case("CatalogMatchesInstruments", "Metric catalog matches every published Tablix instrument", CatalogMatchesInstrumentsAsync),
                    Case("NamingConventions", "Metric names are prefixed, declared in TelemetryNames, and use UCUM units", NamingConventionsAsync),
                    Case("StatementTypeClassification", "Query statement types are bounded labels", StatementTypeClassificationAsync),
                    Case("TelemetrySettingsDefaults", "Telemetry settings default to loopback and clamp invalid values", TelemetrySettingsDefaultsAsync),
                    Case("TelemetryHostSubscribesSources", "Telemetry host subscribes every meter and source with the catalog", TelemetryHostSubscribesSourcesAsync),
                    Case("TelemetryHostFailureIsBestEffort", "Telemetry host start failure and disabled export are non-fatal", TelemetryHostFailureIsBestEffortAsync),
                    Case("CrawlPipelineSuccess", "Crawl job emits job, per-stage, table, and client telemetry", CrawlPipelineSuccessAsync),
                    Case("CrawlPipelineFailure", "Failed crawl emits error outcome, failed stage, and error count", CrawlPipelineFailureAsync),
                    Case("CrawlCacheLookups", "Crawl cache emits hit, miss, and entry gauges", CrawlCacheLookupsAsync),
                    Case("DatabaseClientOperations", "User database query and connectivity operations emit client telemetry", DatabaseClientOperationsAsync),
                    Case("PersistenceAndContext", "Persistence operations, lock wait, and context writes emit telemetry", PersistenceAndContextAsync),
                    Case("ModelCallScopeOutcomes", "Model calls record success, failure, exception, tokens, and redact secrets", ModelCallScopeOutcomesAsync),
                    Case("McpToolCalls", "MCP tool calls continue the caller trace and record outcomes", McpToolCallsAsync),
                    Case("HealthMonitorBackgroundJob", "Health monitor emits root spans, outcomes, transitions, and gauges", HealthMonitorBackgroundJobAsync),
                    Case("EndToEndServer", "Live server exports Tablix and Watson telemetry through Prometheus", EndToEndServerAsync),
                    Case("DashboardsUseEmittedMetrics", "Grafana dashboards query only metric names Tablix actually emits", DashboardsUseEmittedMetricsAsync),
                    Case("ObservabilityStackContract", "Compose stack, provisioning, and the home-page services card stay consistent", ObservabilityStackContractAsync)
                });
        }

        #endregion

        #region Cases

        private static Task NoListenerIsSafeAsync(CancellationToken token)
        {
            // Every recorder and tracing helper must be inert and safe with no listener and with null input.
            TablixMetrics.RecordCrawlJob(null, null, TelemetryNames.OutcomeSuccess, 1, 1);
            TablixMetrics.RecordCrawlStage(TelemetryNames.StageDiscover, null, TelemetryNames.OutcomeError, 0.1);
            TablixMetrics.RecordDbClientOperation(null, TelemetryNames.DbOperationQuery, TelemetryNames.OutcomeSuccess, 0.1);
            TablixMetrics.RecordQuery(TelemetryNames.SourceRest, null, TelemetryNames.OutcomeRejected, 0.1, 0);
            TablixMetrics.RecordChat(TelemetryNames.ChatModeSync, null, TelemetryNames.OutcomeFailure, 1);
            TablixMetrics.RecordChatToolCall(null, null, TelemetryNames.OutcomeError, 1);
            TablixMetrics.RecordModelRequest(null, TelemetryNames.ModelOperationChat, TelemetryNames.OutcomeError, 1);
            TablixMetrics.RecordModelTokens(null, -1, 5);
            TablixMetrics.RecordTimeToFirstToken(null, 0);
            TablixMetrics.RecordMcpToolCall(null, TelemetryNames.OutcomeSuccess, 1);
            TablixMetrics.RecordError(null, (Exception)null);
            TablixMetrics.RecordContextUpdate(TelemetryNames.ScopeTable, TablixMetrics.ContextSource(null), TelemetryNames.OutcomeSuccess);
            TablixMetrics.MarkHealthCycle();

            Activity none = TablixTracing.StartDetachedChild("stage:test", null);
            Null(none, "A detached child without a parent must be null.");
            TablixTracing.SetTag(null, "k", "v");
            TablixTracing.SetSuccess(null);
            TablixTracing.SetOutcome(null, TelemetryNames.OutcomeFailure, "x");
            TablixTracing.RecordException(null, new InvalidOperationException("x"));
            TablixTracing.StopDetached(null);

            True(TablixTracing.ParseTraceParent("garbage", null) == default, "Invalid traceparent must parse to default.");
            True(TablixTracing.ParseTraceParent(null, null) == default, "Missing traceparent must parse to default.");
            Equal(_SampleTraceId, TablixTracing.ParseTraceParent(_SampleTraceParent, null).TraceId.ToHexString(), "Valid traceparent trace id mismatch.");

            using (ModelCallScope scope = ModelCallScope.Start(null, TelemetryNames.ModelOperationChat))
            {
            }

            using (TelemetryHost host = TelemetryHost.Start(null))
            {
                False(host.IsRunning, "A null settings host must not run.");
                host.AttachLogging(new LoggingModule());
            }

            return Task.CompletedTask;
        }

        private static Task CatalogMatchesInstrumentsAsync(CancellationToken token)
        {
            using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName);
            True(TablixMetrics.Meter != null, "Meter must exist.");

            Dictionary<string, Instrument> published = capture.PublishedInstruments()
                .Values
                .Where(i => i.Meter.Name == TelemetryNames.MeterName)
                .ToDictionary(i => i.Name, i => i, StringComparer.Ordinal);
            Dictionary<string, Convention> catalog = TelemetryCatalog.All.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);

            foreach (string name in published.Keys)
                True(catalog.ContainsKey(name), "Instrument '" + name + "' is published but missing from TelemetryCatalog.");
            foreach (string name in catalog.Keys)
                True(published.ContainsKey(name), "Catalog entry '" + name + "' has no published instrument.");

            foreach (KeyValuePair<string, Instrument> entry in published)
            {
                Convention convention = catalog[entry.Key];
                Equal(convention.Unit, entry.Value.Unit, "Unit mismatch for " + entry.Key + ".");
                Equal(ExpectedKind(entry.Value), convention.Kind.ToString(), "Kind mismatch for " + entry.Key + ".");
            }

            return Task.CompletedTask;
        }

        private static Task NamingConventionsAsync(CancellationToken token)
        {
            List<string> declared = typeof(TelemetryNames)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.Name.StartsWith("Metric", StringComparison.Ordinal))
                .Select(f => (string)f.GetRawConstantValue())
                .ToList();

            Equal(TelemetryCatalog.All.Count, declared.Count, "Every metric name must be declared once in TelemetryNames.");
            foreach (Convention convention in TelemetryCatalog.All)
            {
                True(convention.Name.StartsWith("tablix.", StringComparison.Ordinal), "Metric '" + convention.Name + "' must be prefixed with tablix.");
                True(declared.Contains(convention.Name), "Metric '" + convention.Name + "' must be declared in TelemetryNames.");
                if (convention.Kind == MetricKindEnum.Histogram)
                {
                    True(convention.Unit == "s" || convention.Unit == "{row}", "Histogram '" + convention.Name + "' must use seconds or {row}.");
                    True(convention.Buckets != null && convention.Buckets.Length > 0, "Histogram '" + convention.Name + "' must declare buckets.");
                }

                foreach (string label in convention.LabelKeys)
                {
                    True(!label.StartsWith("tablix.", StringComparison.Ordinal), "Unbounded id label '" + label + "' must not be a metric label on " + convention.Name + ".");
                }
            }

            return Task.CompletedTask;
        }

        private static Task StatementTypeClassificationAsync(CancellationToken token)
        {
            Equal("select", QueryValidator.GetStatementType("SELECT * FROM users"), "SELECT mismatch.");
            Equal("select", QueryValidator.GetStatementType("  -- comment\n select 1"), "Commented SELECT mismatch.");
            Equal("delete", QueryValidator.GetStatementType("DELETE FROM users WHERE id = 1"), "DELETE mismatch.");
            Equal("other", QueryValidator.GetStatementType("FROBNICATE everything"), "Unknown keyword must be other.");
            Equal("other", QueryValidator.GetStatementType(null), "Null must be other.");
            Equal("other", QueryValidator.GetStatementType("   "), "Whitespace must be other.");
            return Task.CompletedTask;
        }

        private static Task TelemetrySettingsDefaultsAsync(CancellationToken token)
        {
            TelemetrySettings settings = new TelemetrySettings();
            True(settings.Enable, "Telemetry must be enabled by default.");
            Equal("tablix-server", settings.ServiceName, "Service name default mismatch.");
            Equal("http://127.0.0.1:4317", settings.OtlpEndpoint, "OTLP endpoint must default to 127.0.0.1.");
            Equal("127.0.0.1", settings.PrometheusHostname, "Prometheus hostname must default to 127.0.0.1.");
            Equal(9464, settings.PrometheusPort, "Prometheus port default mismatch.");
            Equal("http://127.0.0.1:3100/otlp", settings.LokiEndpoint, "Loki endpoint must default to 127.0.0.1.");
            False(settings.LokiEnable, "Direct Loki export must default off.");

            settings.OtlpEndpoint = "not a uri";
            Equal("http://127.0.0.1:4317", settings.OtlpEndpoint, "Invalid OTLP endpoint must reset to default.");
            settings.OtlpProtocol = "bogus";
            Equal("grpc", settings.OtlpProtocol, "Invalid protocol must reset to grpc.");
            settings.PrometheusPort = 999999;
            Equal(65535, settings.PrometheusPort, "Prometheus port must clamp.");
            settings.PrometheusPath = "scrape";
            Equal("/scrape", settings.PrometheusPath, "Prometheus path must gain a leading slash.");
            settings.TraceSamplingRatio = 5;
            Equal(1.0, settings.TraceSamplingRatio, "Sampling ratio must clamp to 1.");
            settings.MetricExportIntervalMs = 1;
            Equal(1000, settings.MetricExportIntervalMs, "Export interval must clamp to 1000.");
            settings.ServiceName = " ";
            Equal("tablix-server", settings.ServiceName, "Blank service name must reset.");

            TablixSettings root = new TablixSettings();
            root.Telemetry.PrometheusPort = 9555;
            TablixSettings roundTrip = Serializer.DeserializeJson<TablixSettings>(Serializer.SerializeJson(root, true));
            Equal(9555, roundTrip.Telemetry.PrometheusPort, "Telemetry settings must round-trip through tablix.json.");

            TablixSettings legacy = Serializer.DeserializeJson<TablixSettings>("{\"Rest\":{\"Port\":9100}}");
            NotNull(legacy.Telemetry, "A tablix.json without Telemetry must get defaults.");
            True(legacy.Telemetry.Enable, "Legacy settings telemetry default mismatch.");
            return Task.CompletedTask;
        }

        private static Task TelemetryHostSubscribesSourcesAsync(CancellationToken token)
        {
            RadiantSettings radiant = TelemetryHost.BuildRadiantSettings(new TelemetrySettings());
            True(radiant.Sources.MeterNames.Contains(TelemetryNames.MeterName), "Tablix meter must be subscribed.");
            True(radiant.Sources.MeterNames.Contains(TelemetryNames.WatsonSourceName), "Watson meter must be subscribed.");
            True(radiant.Sources.MeterNames.Contains(TelemetryNames.HttpClientMeterName), "HTTP client meter must be subscribed.");
            True(radiant.Sources.ActivitySourceNames.Contains(TelemetryNames.ActivitySourceName), "Tablix source must be subscribed.");
            True(radiant.Sources.ActivitySourceNames.Contains(TelemetryNames.WatsonSourceName), "Watson source must be subscribed.");
            Equal(TelemetryCatalog.All.Count + 1, radiant.Metrics.Definitions.Count, "Catalog and the Watson duration view must be registered.");
            MetricDefinition watsonDuration = radiant.Metrics.Definitions.Single(d => d.Key == TelemetryHost.HttpServerRequestDuration);
            True(watsonDuration.Buckets != null && watsonDuration.Buckets[0] < 0.01, "Watson HTTP duration must get seconds-scale buckets.");
            Equal("127.0.0.1", radiant.Prometheus.Hostname, "Prometheus must bind loopback by default.");
            True(radiant.Metrics.IncludeRuntime, "Runtime metrics must be included.");
            return Task.CompletedTask;
        }

        private static Task TelemetryHostFailureIsBestEffortAsync(CancellationToken token)
        {
            TelemetryHost disabled = TelemetryHost.Start(new TelemetrySettings { Enable = false });
            False(disabled.IsRunning, "Disabled telemetry must not run.");
            NotNull(disabled.StatusMessage, "Disabled telemetry must explain why.");
            disabled.Dispose();
            disabled.Dispose();

            int port = FreePort();
            TelemetrySettings settings = new TelemetrySettings { OtlpEnable = false, PrometheusPort = port, ExportLogs = false };
            using TelemetryHost first = TelemetryHost.Start(settings);
            True(first.IsRunning, "First host must run: " + first.StatusMessage);
            NotNull(first.PrometheusUrl, "Running host must report its scrape URL.");

            // A second host on the same scrape port fails inside Radiant; the server must keep running without export.
            using TelemetryHost second = TelemetryHost.Start(settings);
            False(second.IsRunning, "Second host on the same port must not run.");
            NotNull(second.StatusMessage, "Start failure must be reported.");
            return Task.CompletedTask;
        }

        private static async Task CrawlPipelineSuccessAsync(CancellationToken token)
        {
            await WithSampleDatabaseAsync(async entry =>
            {
                using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName);
                CrawlCache cache = new CrawlCache();
                await cache.CrawlAllAsync(new List<DatabaseEntry> { entry }, TelemetryNames.TriggerStartup).ConfigureAwait(false);
                True(cache.Get(entry.Id).IsCrawled, "Crawl should succeed.");

                Equal(1.0, capture.Sum(TelemetryNames.MetricCrawlJobs, "trigger=startup", "db.system.name=sqlite", "outcome=success"), "Crawl job count mismatch.");
                Equal(1, capture.Find(TelemetryNames.MetricCrawlDuration, "outcome=success").Count, "Crawl duration must be recorded once.");
                foreach (string stage in new[] { TelemetryNames.StageQueued, TelemetryNames.StageDiscover, TelemetryNames.StageExamine, TelemetryNames.StageCache })
                {
                    Equal(1, capture.Find(TelemetryNames.MetricCrawlStageDuration, "stage=" + stage, "outcome=success").Count, "Stage '" + stage + "' duration missing.");
                    Equal(1.0, capture.Sum(TelemetryNames.MetricCrawlStageEvents, "stage=" + stage, "outcome=success"), "Stage '" + stage + "' event missing.");
                }

                True(capture.Sum(TelemetryNames.MetricCrawlTables, "db.system.name=sqlite") > 0, "Discovered tables must be counted.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricDbClientOperations, "db.operation.name=crawl", "outcome=success"), "Crawl client operation missing.");
                True(capture.Sum(TelemetryNames.MetricCrawlActive) == 0, "Active crawl count must return to zero.");

                capture.CollectObservables();
                True(capture.Find(TelemetryNames.MetricCrawlLastSuccess, "db.system.name=sqlite").Any(m => m.Value > 0), "Last success gauge missing.");

                Activity job = capture.Spans(TelemetryNames.SpanCrawlJob).Single();
                Equal(ActivityStatusCode.Ok, job.Status, "Job span status mismatch.");
                Equal(entry.Id, job.GetTagItem(TelemetryNames.AttrDatabaseId) as string, "Job span database id mismatch.");
                List<string> children = capture.Children(job).Select(a => a.DisplayName).ToList();
                foreach (string expected in new[] { "stage:queued", "stage:discover", "stage:examine", "stage:cache", "sqlite crawl" })
                    True(children.Contains(expected), "Job span is missing child '" + expected + "'. Children: " + String.Join(", ", children));

                AssertLabelsDeclared(capture);
            }).ConfigureAwait(false);
        }

        private static async Task CrawlPipelineFailureAsync(CancellationToken token)
        {
            using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName);
            CrawlCache cache = new CrawlCache();
            DatabaseEntry entry = new DatabaseEntry
            {
                Id = "telemetry_missing_db",
                Type = DatabaseTypeEnum.Sqlite,
                Filename = Path.Combine(Path.GetTempPath(), "missing_dir_" + Guid.NewGuid().ToString("N"), "missing.db")
            };

            DatabaseDetail detail = await cache.CrawlOneAsync(entry, TelemetryNames.TriggerApi).ConfigureAwait(false);
            False(detail.IsCrawled, "Crawl should be degraded.");

            Equal(1.0, capture.Sum(TelemetryNames.MetricCrawlJobs, "trigger=api", "outcome=error"), "Failed job must be counted as error.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricCrawlStageEvents, "stage=discover", "outcome=error"), "The failing stage must be discover.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricErrors, "component=crawl"), "Crawl error must be counted.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricDbClientOperations, "db.operation.name=crawl", "outcome=error"), "Client operation error missing.");

            Activity job = capture.Spans(TelemetryNames.SpanCrawlJob).Single();
            Equal(ActivityStatusCode.Error, job.Status, "Failed job span must be an error.");
            NotNull(job.GetTagItem(TelemetryNames.AttrErrorType), "Failed job span must carry error.type.");
            True(job.Events.Any(e => e.Name == "exception"), "Failed job span must record the exception event.");
            AssertLabelsDeclared(capture);
        }

        private static async Task CrawlCacheLookupsAsync(CancellationToken token)
        {
            await WithSampleDatabaseAsync(async entry =>
            {
                CrawlCache cache = new CrawlCache();
                await cache.CrawlOneAsync(entry).ConfigureAwait(false);

                using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName);
                NotNull(cache.Get(entry.Id), "Cached entry missing.");
                Null(cache.Get("telemetry_absent"), "Absent entry must be null.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricCrawlCacheLookups, "result=hit"), "Cache hit missing.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricCrawlCacheLookups, "result=miss"), "Cache miss missing.");

                Func<CrawlCacheCounts> previous = TablixMetrics.CrawlCacheCountsProvider;
                try
                {
                    TablixMetrics.CrawlCacheCountsProvider = () => cache.GetCounts();
                    capture.CollectObservables();
                    Equal(1.0, capture.Sum(TelemetryNames.MetricCrawlCacheEntries, "state=crawled"), "Crawled entry gauge mismatch.");
                    Equal(0.0, capture.Sum(TelemetryNames.MetricCrawlCacheEntries, "state=degraded"), "Degraded entry gauge mismatch.");
                }
                finally
                {
                    TablixMetrics.CrawlCacheCountsProvider = previous;
                }
            }).ConfigureAwait(false);
        }

        private static async Task DatabaseClientOperationsAsync(CancellationToken token)
        {
            await WithSampleDatabaseAsync(async entry =>
            {
                using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName);
                IDatabaseCrawler crawler = CrawlerFactory.Create(DatabaseTypeEnum.Sqlite);

                QueryResult ok = await crawler.ExecuteQueryAsync(entry, "SELECT * FROM users", token).ConfigureAwait(false);
                True(ok.Success, "Query should succeed.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricDbClientOperations, "db.system.name=sqlite", "db.operation.name=query", "outcome=success"), "Query success missing.");

                bool threw = false;
                QueryResult bad = null;
                try
                {
                    bad = await crawler.ExecuteQueryAsync(entry, "SELECT no_such_column FROM users", token).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    threw = true;
                }

                True(threw || (bad != null && !bad.Success), "Bad query should fail.");
                Equal(1.0, capture.Find(TelemetryNames.MetricDbClientOperations, "db.operation.name=query").Count(m => m.Tags[TelemetryNames.AttrOutcome] != TelemetryNames.OutcomeSuccess), "Query failure missing.");

                await crawler.TestConnectionAsync(entry, token).ConfigureAwait(false);
                Equal(1.0, capture.Sum(TelemetryNames.MetricDbClientOperations, "db.operation.name=test_connection", "outcome=success"), "Connectivity test missing.");

                Activity query = capture.Spans("sqlite query").First(a => a.Status == ActivityStatusCode.Ok);
                Equal(ActivityKind.Client, query.Kind, "Query span must be a client span.");
                Equal("select", query.GetTagItem(TelemetryNames.AttrStatement) as string, "Query span statement mismatch.");
                NotNull(query.GetTagItem(TelemetryNames.AttrRowCount), "Query span must carry the row count.");
                foreach (Activity span in capture.AllSpans())
                    True(!span.Tags.Any(t => t.Value != null && t.Value.Contains("SELECT *", StringComparison.OrdinalIgnoreCase)), "Query text must never be recorded on spans.");

                AssertLabelsDeclared(capture);
            }).ConfigureAwait(false);
        }

        private static async Task PersistenceAndContextAsync(CancellationToken token)
        {
            string filename = Path.Combine(Path.GetTempPath(), "tablix_telemetry_" + Guid.NewGuid().ToString("N") + ".db");
            try
            {
                SqliteDatabaseDriver driver = new SqliteDatabaseDriver(filename);
                await driver.InitializeAsync(token).ConfigureAwait(false);

                using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName);
                await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "telemetry_ctx_db", Name = "Ctx" }, token).ConfigureAwait(false);
                await driver.DatabaseContexts.UpsertAsync("telemetry_ctx_db", "Orders live in the orders table.", "replace", "mcp", token).ConfigureAwait(false);

                bool rejected = false;
                try
                {
                    await driver.TableContexts.UpsertAsync("telemetry_ctx_db", "missing_table", "x", "replace", "chat", token).ConfigureAwait(false);
                }
                catch (KeyNotFoundException)
                {
                    rejected = true;
                }

                True(rejected, "Unknown table context write must be rejected.");
                True(capture.Sum(TelemetryNames.MetricPersistenceOperations, "db.operation.name=write", "outcome=success") >= 2, "Persistence writes missing.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricPersistenceOperations, "db.operation.name=write", "outcome=rejected"), "Rejected persistence write missing.");
                Equal(0.0, capture.Sum(TelemetryNames.MetricErrors, "component=persistence"), "Domain validation must not count as a persistence error.");
                True(capture.Find(TelemetryNames.MetricPersistenceLockWait, "db.operation.name=write").Count >= 2, "Lock wait must be recorded.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricContextUpdates, "scope=database", "source=mcp", "outcome=success"), "Database context write missing.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricContextUpdates, "scope=table", "source=chat", "outcome=error"), "Failed table context write missing.");

                List<Activity> writes = capture.Spans(TelemetryNames.SpanPersistencePrefix + TelemetryNames.PersistenceWrite);
                True(writes.Any(a => a.Status == ActivityStatusCode.Ok && (a.GetTagItem(TelemetryNames.AttrCodeFunction) as string) == "UpsertAsync"), "Persistence span must name the calling function.");
                True(writes.Any(a => (a.GetTagItem(TelemetryNames.AttrOutcome) as string) == TelemetryNames.OutcomeRejected), "Rejected persistence span missing.");
                AssertLabelsDeclared(capture);
                driver.Dispose();
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                TryDelete(filename);
            }
        }

        private static Task ModelCallScopeOutcomesAsync(CancellationToken token)
        {
            using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName);
            ModelProviderSettings provider = new ModelProviderSettings
            {
                Id = "telemetry_provider",
                Type = ModelProviderTypeEnum.OpenAI,
                Endpoint = "http://127.0.0.1:1/v1",
                ApiKey = "sk-telemetry-secret-123",
                Model = "fake-model"
            };

            using (ModelCallScope scope = ModelCallScope.Start(provider, TelemetryNames.ModelOperationChatStream))
            {
                scope.Complete(true, "fake-model", 200, new TokenUsage { PromptTokens = 7, CompletionTokens = 3 }, 120, null);
            }

            using (ModelCallScope scope = ModelCallScope.Start(provider, TelemetryNames.ModelOperationChat))
            {
                scope.Complete(false, null, 401, null, 0, "invalid key sk-telemetry-secret-123");
            }

            using (ModelCallScope scope = ModelCallScope.Start(provider, TelemetryNames.ModelOperationToolChat))
            {
                scope.Complete(new TimeoutException("timed out"));
            }

            using (ModelCallScope scope = ModelCallScope.Start(provider, TelemetryNames.ModelOperationToolChat))
            {
                scope.Complete(new OperationCanceledException());
            }

            using (ModelCallScope scope = ModelCallScope.Start(provider, TelemetryNames.ModelOperationChat))
            {
                // Never completed: the call threw past the scope.
            }

            Equal(1.0, capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.provider.name=openai", "gen_ai.operation.name=chat_stream", "outcome=success"), "Success missing.");
            Equal(2.0, capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.operation.name=chat", "outcome=failure") + capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.operation.name=chat", "outcome=error"), "Failure and uncompleted error missing.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.operation.name=tool_chat", "outcome=error"), "Exception error missing.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.operation.name=tool_chat", "outcome=canceled"), "Canceled missing.");
            Equal(7.0, capture.Sum(TelemetryNames.MetricModelTokens, "gen_ai.token.type=input"), "Input tokens mismatch.");
            Equal(3.0, capture.Sum(TelemetryNames.MetricModelTokens, "gen_ai.token.type=output"), "Output tokens mismatch.");
            Equal(0.12, Math.Round(capture.Sum(TelemetryNames.MetricChatTimeToFirstToken), 2), "Time to first token mismatch.");
            Equal(5, capture.Find(TelemetryNames.MetricModelRequestDuration).Count, "Every call must record a duration.");

            List<Activity> spans = capture.AllSpans().Where(a => a.DisplayName.StartsWith("openai ", StringComparison.Ordinal)).ToList();
            Equal(5, spans.Count, "Every call must produce a client span.");
            True(spans.All(a => a.Kind == ActivityKind.Client), "Model spans must be client spans.");
            Activity failed = spans.Single(a => (a.GetTagItem("http.response.status_code") as int?) == 401);
            Equal(ActivityStatusCode.Error, failed.Status, "Failed call status mismatch.");
            DoesNotContain(failed.StatusDescription ?? String.Empty, "sk-telemetry-secret-123", "API keys must be redacted from span status.");
            foreach (Activity span in spans)
            {
                foreach (KeyValuePair<string, string> tag in span.Tags)
                    DoesNotContain(tag.Value ?? String.Empty, "sk-telemetry-secret-123", "API keys must never be recorded on spans.");
            }

            AssertLabelsDeclared(capture);
            return Task.CompletedTask;
        }

        private static async Task McpToolCallsAsync(CancellationToken token)
        {
            using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName);
            RpcCallContext context = new RpcCallContext(null, new Dictionary<string, string> { [McpTelemetry.TraceParentClaim] = _SampleTraceParent });

            object ok = await McpTelemetry.InvokeToolAsync("tablix_discover_databases", context, () => Task.FromResult<object>(new EnumerationResult<DatabaseSummary> { Success = true })).ConfigureAwait(false);
            NotNull(ok, "Result must pass through.");
            await McpTelemetry.InvokeToolAsync("tablix_execute_query", null, () => Task.FromResult<object>(new QueryResult { Success = false, Error = "denied" })).ConfigureAwait(false);

            bool rethrown = false;
            try
            {
                await McpTelemetry.InvokeToolAsync("tablix_get_agent_pack", null, () => throw new InvalidOperationException("boom")).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                rethrown = true;
            }

            True(rethrown, "Tool exceptions must be rethrown unchanged.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricMcpToolCalls, "gen_ai.tool.name=tablix_discover_databases", "outcome=success"), "MCP success missing.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricMcpToolCalls, "gen_ai.tool.name=tablix_execute_query", "outcome=failure"), "MCP failure result missing.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricMcpToolCalls, "gen_ai.tool.name=tablix_get_agent_pack", "outcome=error"), "MCP exception missing.");
            Equal(1.0, capture.Sum(TelemetryNames.MetricErrors, "component=mcp", "error.type=InvalidOperationException"), "MCP error count missing.");
            Equal(0.0, capture.Sum(TelemetryNames.MetricMcpToolActive), "Active MCP calls must return to zero.");

            Activity continued = capture.Spans("tools/call tablix_discover_databases").Single();
            Equal(ActivityKind.Server, continued.Kind, "MCP span must be a server span.");
            Equal(_SampleTraceId, continued.TraceId.ToHexString(), "MCP span must continue the caller's trace.");
            Equal(_SampleSpanId, continued.ParentSpanId.ToHexString(), "MCP span must be parented on the caller's span.");
            Activity root = capture.Spans("tools/call tablix_execute_query").Single();
            True(root.ParentSpanId == default, "Without traceparent the MCP span must be a root.");
            AssertLabelsDeclared(capture);
        }

        private static async Task HealthMonitorBackgroundJobAsync(CancellationToken token)
        {
            string filename = Path.Combine(Path.GetTempPath(), "tablix_telemetry_" + Guid.NewGuid().ToString("N") + ".db");
            Func<ProviderHealthCounts> previous = TablixMetrics.ProviderHealthCountsProvider;
            using FakeModelProvider fake = new FakeModelProvider();
            SqliteDatabaseDriver driver = new SqliteDatabaseDriver(filename);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            ModelProviderHealthCheckService service = new ModelProviderHealthCheckService(driver, new LoggingModule { Settings = { EnableConsole = false } });
            try
            {
                using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName);
                service.OnProviderSaved(new ModelProviderSettings
                {
                    Id = "telemetry_health",
                    Name = "Health",
                    Type = ModelProviderTypeEnum.Ollama,
                    Endpoint = fake.RootUrl,
                    Model = "fake-model",
                    HealthCheckEnabled = true,
                    HealthCheckUrl = fake.RootUrl,
                    HealthCheckIntervalMs = 1000,
                    HealthCheckTimeoutMs = 2000,
                    HealthCheckExpectedStatusCode = 200,
                    HealthyThreshold = 1,
                    UnhealthyThreshold = 1
                });
                TablixMetrics.ProviderHealthCountsProvider = () => service.GetHealthCounts();

                // The monitor runs on a background loop started from inside an unrelated span; checks must still be roots.
                using (Activity unrelated = TablixTracing.Start("unrelated.parent"))
                {
                    service.Start(token);
                }

                await WaitUntilAsync(() => capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.operation.name=health_check", "outcome=success") >= 1, token).ConfigureAwait(false);
                fake.StatusCode = 503;
                await WaitUntilAsync(() => service.GetHealthCounts().Unhealthy == 1, token).ConfigureAwait(false);
                True(capture.Sum(TelemetryNames.MetricModelHealthTransitions, "gen_ai.provider.name=ollama", "state=unhealthy") >= 1, "Unhealthy transition missing.");

                True(capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.provider.name=ollama", "gen_ai.operation.name=health_check", "outcome=failure") >= 1, "Health check failure missing.");

                capture.CollectObservables();
                True(capture.Sum(TelemetryNames.MetricModelProviders, "state=unhealthy") >= 1, "Unhealthy provider gauge mismatch.");
                True(capture.Find(TelemetryNames.MetricModelHealthLastCycle).Any(m => m.Value > 0), "Health monitor heartbeat gauge missing.");

                List<Activity> checks = capture.Spans("ollama health_check");
                True(checks.Count >= 2, "Each check must produce a span.");
                True(checks.All(a => a.ParentSpanId == default), "Background health checks must be root spans.");
                Activity failedCheck = checks.First(a => a.Status == ActivityStatusCode.Error);
                Equal("unexpected_status", failedCheck.GetTagItem(TelemetryNames.AttrErrorType) as string, "Failed check error.type mismatch.");
                AssertLabelsDeclared(capture);
            }
            finally
            {
                TablixMetrics.ProviderHealthCountsProvider = previous;
                await service.StopAsync().ConfigureAwait(false);
                service.Dispose();
                driver.Dispose();
                SqliteConnection.ClearAllPools();
                TryDelete(filename);
            }
        }

        private static async Task EndToEndServerAsync(CancellationToken token)
        {
            string directory = Path.Combine(Path.GetTempPath(), "tablix_telemetry_server_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string settingsFile = Path.Combine(directory, "tablix.json");
            string sampleDb = Path.Combine(directory, "sample.db");
            File.Copy(FindSampleDatabase(), sampleDb, true);

            TablixSettings settings = new TablixSettings();
            settings.Rest.Hostname = "127.0.0.1";
            settings.Rest.Port = FreePort();
            settings.Rest.McpPort = FreePort();
            settings.Persistence.Filename = "tablix.db";
            settings.Logging.ConsoleLogging = false;
            settings.Logging.FileLogging = false;
            settings.Telemetry.OtlpEnable = false;
            settings.Telemetry.PrometheusEnable = true;
            settings.Telemetry.PrometheusHostname = "127.0.0.1";
            settings.Telemetry.PrometheusPort = FreePort();
            File.WriteAllText(settingsFile, Serializer.SerializeJson(settings, true));

            string restUrl = "http://127.0.0.1:" + settings.Rest.Port;
            string mcpUrl = "http://127.0.0.1:" + settings.Rest.McpPort + "/mcp";
            string metricsUrl = "http://127.0.0.1:" + settings.Telemetry.PrometheusPort + "/metrics";

            using FakeModelProvider fake = new FakeModelProvider();
            using TelemetryCapture capture = new TelemetryCapture(TelemetryNames.MeterName, TelemetryNames.ActivitySourceName, TelemetryNames.WatsonSourceName);
            using HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer tablixadmin");
            TablixServer server = new TablixServer(settingsFile);
            try
            {
                await server.StartAsync(token).ConfigureAwait(false);
                True(server.Telemetry.IsRunning, "Telemetry host must run: " + server.Telemetry.StatusMessage);
                Equal(metricsUrl, server.Telemetry.PrometheusUrl, "Scrape URL mismatch.");
                await WaitForOkAsync(http, restUrl + "/", token).ConfigureAwait(false);

                // Provider and database.
                string providerJson = await SendAsync(http, HttpMethod.Post, restUrl + "/v1/model",
                    "{\"Name\":\"Fake\",\"Type\":\"OpenAI\",\"Endpoint\":\"" + fake.BaseUrl + "\",\"ApiKey\":\"sk-e2e-secret\",\"Model\":\"fake-model\",\"HealthCheckEnabled\":false}", null, 201, token).ConfigureAwait(false);
                string providerId;
                using (JsonDocument document = JsonDocument.Parse(providerJson)) providerId = document.RootElement.GetProperty("Id").GetString();

                await SendAsync(http, HttpMethod.Post, restUrl + "/v1/database",
                    "{\"Id\":\"telemetry_db\",\"Name\":\"Telemetry\",\"Type\":\"Sqlite\",\"Filename\":" + JsonSerializer.Serialize(sampleDb) + ",\"AllowedQueries\":[\"SELECT\"]}", null, 201, token).ConfigureAwait(false);
                await WaitUntilAsync(() => capture.Sum(TelemetryNames.MetricCrawlJobs, "trigger=database_added") >= 1, token).ConfigureAwait(false);

                // Queries: allowed and rejected.
                await SendAsync(http, HttpMethod.Post, restUrl + "/v1/database/telemetry_db/query", "{\"Query\":\"SELECT COUNT(*) FROM users\"}", null, 200, token).ConfigureAwait(false);
                await SendAsync(http, HttpMethod.Post, restUrl + "/v1/database/telemetry_db/query", "{\"Query\":\"DELETE FROM users\"}", null, 403, token).ConfigureAwait(false);

                // Chat: success then provider failure, each from a caller trace.
                string chatBody = "{\"DatabaseId\":\"telemetry_db\",\"ProviderId\":\"" + providerId + "\",\"Messages\":[{\"Role\":\"user\",\"Content\":\"Say hello.\"}]}";
                await SendAsync(http, HttpMethod.Post, restUrl + "/v1/chat", chatBody, _SampleTraceParent, 200, token).ConfigureAwait(false);

                // Table context build: parallel tables, each with its own span and stages.
                await SendAsync(http, HttpMethod.Post, restUrl + "/v1/database/telemetry_db/table-context/build", "{\"ProviderId\":\"" + providerId + "\"}", null, 200, token).ConfigureAwait(false);
                fake.StatusCode = 500;
                await SendAsync(http, HttpMethod.Post, restUrl + "/v1/chat", chatBody, null, 502, token).ConfigureAwait(false);

                // MCP tool call from a caller trace.
                McpTestSession mcp = await McpTestSession.OpenAsync(mcpUrl, token).ConfigureAwait(false);
                string mcpResponse = await mcp.PostAsync(
                    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/call\",\"params\":{\"name\":\"tablix_discover_databases\",\"arguments\":{}}}",
                    token,
                    "00-" + _SampleTraceId.Replace('4', '5') + "-" + _SampleSpanId + "-01").ConfigureAwait(false);
                True(mcpResponse.Contains("\"result\"", StringComparison.Ordinal), "MCP call failed: " + mcpResponse);

                // Metrics through the real Prometheus exporter.
                string scrape = await WaitForScrapeAsync(http, metricsUrl, "tablix_mcp_tool_calls_total", token).ConfigureAwait(false);
                foreach (string family in new[]
                {
                    "tablix_chat_requests_total", "tablix_chat_stage_duration_seconds_bucket", "tablix_model_requests_total",
                    "tablix_query_executions_total", "tablix_crawl_jobs_total", "tablix_crawl_stage_duration_seconds_bucket",
                    "tablix_db_client_operations_total", "tablix_persistence_operations_total", "tablix_mcp_tool_calls_total",
                    "tablix_build_info", "tablix_config_flag", "http_server_request_duration_seconds_bucket"
                })
                {
                    Contains(scrape, family, "Prometheus scrape is missing " + family + ".");
                }

                True(scrape.Split('\n').Any(line => line.StartsWith("http_server_request_duration_seconds_bucket", StringComparison.Ordinal) && line.Contains("le=\"0.005", StringComparison.Ordinal)), "Watson HTTP duration must export seconds-scale buckets.");
                DoesNotContain(scrape, "sk-e2e-secret", "Secrets must never reach metrics.");
                DoesNotContain(scrape, "telemetry_db", "Database ids must never be metric labels.");

                // In-process measurements.
                Equal(1.0, capture.Sum(TelemetryNames.MetricChatRequests, "mode=sync", "outcome=success"), "Chat success missing.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricChatRequests, "mode=sync", "outcome=failure"), "Chat provider failure missing.");
                True(capture.Find(TelemetryNames.MetricChatStageDuration, "stage=prepare", "outcome=success").Count >= 2, "Prepare stage missing.");
                True(capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.provider.name=openai", "outcome=success") >= 1, "Model success missing.");
                True(capture.Sum(TelemetryNames.MetricModelRequests, "gen_ai.provider.name=openai", "outcome=failure") >= 1, "Model failure missing.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricQueryExecutions, "source=rest", "statement=select", "outcome=success"), "REST query success missing.");
                Equal(1.0, capture.Sum(TelemetryNames.MetricQueryExecutions, "source=rest", "statement=delete", "outcome=rejected"), "REST query rejection missing.");
                True(capture.Find("http.server.request.duration").Count > 0, "Watson HTTP metrics must be emitted.");

                // Traces: Watson root, chat workflow beneath it, caller trace adopted, model call beneath chat.
                Activity chat = capture.Spans(TelemetryNames.SpanChat).First(a => a.Status == ActivityStatusCode.Ok);
                Equal(_SampleTraceId, chat.TraceId.ToHexString(), "Chat must continue the caller's trace through Watson.");
                Activity watsonSpan = capture.AllSpans().FirstOrDefault(a => a.SpanId == chat.ParentSpanId);
                NotNull(watsonSpan, "Chat span must be a child of the Watson request span.");
                Equal(TelemetryNames.WatsonSourceName, watsonSpan.Source.Name, "Chat parent must come from Watson.");
                List<Activity> chatTrace = capture.AllSpans().Where(a => a.TraceId == chat.TraceId).ToList();
                True(chatTrace.Any(a => a.DisplayName == "stage:prepare"), "Prepare stage span missing.");
                True(chatTrace.Any(a => a.DisplayName.StartsWith("openai ", StringComparison.Ordinal) && a.Kind == ActivityKind.Client), "Model client span missing from chat trace.");
                True(fake.TraceParents.Any(t => t != null && t.Contains(_SampleTraceId, StringComparison.Ordinal)), "Outbound model calls must propagate the W3C trace context.");

                Activity failedChat = capture.Spans(TelemetryNames.SpanChat).First(a => a.Status != ActivityStatusCode.Ok);
                Equal(ActivityStatusCode.Error, failedChat.Status, "Failed chat span must be an error.");

                Equal(1.0, capture.Sum(TelemetryNames.MetricContextBuilds, "scope=table", "outcome=success"), "Table context build missing.");
                foreach (string stage in new[] { TelemetryNames.StageQueued, TelemetryNames.StageInference, TelemetryNames.StagePersist })
                    True(capture.Find(TelemetryNames.MetricContextBuildStageDuration, "scope=table", "stage=" + stage, "outcome=success").Count >= 3, "Context build stage '" + stage + "' must be recorded per table.");
                True(capture.Sum(TelemetryNames.MetricContextUpdates, "scope=table", "source=model", "outcome=success") >= 3, "Model context writes missing.");
                Activity build = capture.Spans(TelemetryNames.SpanContextBuild).Single();
                List<Activity> tables = capture.Children(build).Where(a => a.DisplayName == TelemetryNames.SpanContextTable).ToList();
                True(tables.Count >= 3, "Each table must get its own context.table span.");
                Equal(tables.Count, tables.Select(a => a.GetTagItem(TelemetryNames.AttrTableId) as string).Distinct().Count(), "Each table span must carry its own table id.");
                foreach (Activity table in tables)
                {
                    List<string> stages = capture.Children(table).Select(a => a.DisplayName).ToList();
                    foreach (string expected in new[] { "stage:queued", "stage:inference", "stage:persist" })
                        True(stages.Contains(expected), "context.table is missing " + expected + ".");
                }

                Activity mcpSpan = capture.Spans("tools/call tablix_discover_databases").Single();
                Equal(_SampleTraceId.Replace('4', '5'), mcpSpan.TraceId.ToHexString(), "MCP must continue the caller's trace over HTTP.");

                Activity batch = capture.Spans(TelemetryNames.SpanCrawlBatch).First(a => (a.GetTagItem(TelemetryNames.AttrTrigger) as string) == TelemetryNames.TriggerDatabaseAdded);
                True(batch.ParentSpanId == default, "Background crawl after add must be a root trace.");
                True(batch.Links.Any(), "Background crawl must link to the request that scheduled it.");

                Activity startup = capture.Spans(TelemetryNames.SpanStartup).Single();
                List<string> startupStages = capture.Children(startup).Select(a => a.DisplayName).ToList();
                foreach (string stage in new[] { "stage:persistence", "stage:logging", "stage:crawl_cache", "stage:health_checks", "stage:rest", "stage:mcp" })
                    True(startupStages.Contains(stage), "Startup is missing " + stage + ".");
                True(capture.AllSpans().Where(a => a.Source.Name == TelemetryNames.WatsonSourceName).All(a => a.TraceId != startup.TraceId), "Requests must never join the startup trace.");

                AssertLabelsDeclared(capture);
            }
            finally
            {
                await server.StopAsync().ConfigureAwait(false);
                SqliteConnection.ClearAllPools();
                TryDeleteDirectory(directory);
            }
        }

        private static Task DashboardsUseEmittedMetricsAsync(CancellationToken token)
        {
            HashSet<string> emitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (Convention convention in TelemetryCatalog.All)
            {
                string baseName = convention.Name.Replace('.', '_') + (convention.Unit == "s" ? "_seconds" : String.Empty);
                emitted.Add(convention.Kind == MetricKindEnum.Counter ? baseName + "_total" : baseName);
            }

            string directory = Path.Combine(FindRepositoryRoot(), "assets", "grafana");
            string[] files = Directory.GetFiles(directory, "*.json");
            True(files.Length >= 6, "Expected the domain dashboards in assets/grafana.");
            HashSet<string> uids = new HashSet<string>(StringComparer.Ordinal);
            int referenced = 0;
            foreach (string file in files)
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
                JsonElement root = document.RootElement;
                True(uids.Add(root.GetProperty("uid").GetString()), "Dashboard uid must be unique: " + file);
                True(root.GetProperty("tags").EnumerateArray().Any(t => t.GetString() == "tablix"), "Dashboard must be tagged tablix: " + file);
                foreach (JsonElement panel in root.GetProperty("panels").EnumerateArray())
                {
                    if (!panel.TryGetProperty("targets", out JsonElement targets)) continue;
                    foreach (JsonElement target in targets.EnumerateArray())
                    {
                        string uid = target.GetProperty("datasource").GetProperty("uid").GetString();
                        True(uid == "prometheus" || uid == "tempo" || uid == "loki", "Panel datasource must use a provisioned uid: " + uid);
                        if (!target.TryGetProperty("expr", out JsonElement expr) || uid != "prometheus") continue;
                        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(expr.GetString(), "tablix_[a-z_]+"))
                        {
                            string name = System.Text.RegularExpressions.Regex.Replace(match.Value, "_(bucket|count|sum)$", String.Empty);
                            True(emitted.Contains(name), Path.GetFileName(file) + " queries '" + match.Value + "', which Tablix does not emit.");
                            referenced++;
                        }
                    }
                }
            }

            True(referenced > 50, "Dashboards should reference the Tablix metric families.");
            return Task.CompletedTask;
        }

        private static Task ObservabilityStackContractAsync(CancellationToken token)
        {
            string root = FindRepositoryRoot();
            string compose = File.ReadAllText(Path.Combine(root, "docker", "compose.yaml"));
            string prometheus = File.ReadAllText(Path.Combine(root, "docker", "prometheus.yaml"));
            string datasources = File.ReadAllText(Path.Combine(root, "docker", "grafana", "provisioning", "datasources", "tablix-datasources.yaml"));
            string providers = File.ReadAllText(Path.Combine(root, "docker", "grafana", "provisioning", "dashboards", "tablix-dashboards.yaml"));
            string settingsJson = File.ReadAllText(Path.Combine(root, "docker", "tablix.json"));
            string factoryJson = File.ReadAllText(Path.Combine(root, "docker", "factory", "tablix.json"));
            string card = File.ReadAllText(Path.Combine(root, "dashboard", "src", "components", "ExternalServicesCard.tsx"));
            string home = File.ReadAllText(Path.Combine(root, "dashboard", "src", "pages", "DatabaseListPage.tsx"));

            foreach (string image in new[] { "otel/opentelemetry-collector-contrib:0.109.0", "prom/prometheus:v3.14.0", "grafana/tempo:2.6.1", "grafana/loki:3.3.0", "grafana/grafana-oss:13.0.2" })
                Contains(compose, "image: " + image, "Compose must pin " + image + ".");
            DoesNotContain(compose, ":latest", "Compose images must be pinned.");
            DoesNotContain(compose, "http://localhost", "Healthchecks must probe 127.0.0.1, not localhost.");
            Contains(compose, "condition: service_healthy", "Dependents must wait for healthy dependencies.");
            Contains(compose, "GF_SECURITY_ADMIN_PASSWORD: \"${GRAFANA_ADMIN_PASSWORD:-admin}\"", "Grafana password must be overridable out of band.");
            Contains(compose, "GF_USERS_ALLOW_SIGN_UP: \"false\"", "Grafana sign-up must be disabled.");
            Contains(compose, "../assets/grafana:/var/lib/grafana/dashboards/tablix:ro", "Dashboards must be provisioned from assets/grafana.");
            Contains(prometheus, "tablix-server:9464", "Prometheus must scrape the Tablix endpoint.");
            Contains(prometheus, "PrometheusText0.0.4", "Prometheus must scrape the classic text format.");
            foreach (string uid in new[] { "uid: prometheus", "uid: tempo", "uid: loki" })
                Contains(datasources, uid, "Datasource " + uid + " must be provisioned with a stable uid.");
            Contains(providers, "folder: 'Tablix'", "Dashboards must load into the Tablix folder.");

            foreach (string json in new[] { settingsJson, factoryJson })
            {
                TablixSettings settings = Serializer.DeserializeJson<TablixSettings>(json);
                Equal("tablix-server", settings.Telemetry.PrometheusHostname, "Docker settings must bind the scrape endpoint to the service name.");
                Equal("http://otel-collector:4317", settings.Telemetry.OtlpEndpoint, "Docker settings must export to the collector.");
                Equal(9464, settings.Telemetry.PrometheusPort, "Docker settings scrape port mismatch.");
            }

            Contains(home, "<ExternalServicesCard />", "The home page must carry the external services card.");
            foreach (string port in new[] { "Port: 3000", "Port: 9090", "Port: 3200", "Port: 3100", "Port: 4318" })
                Contains(card, port, "The services card must list " + port + ".");
            foreach (string published in new[] { "127.0.0.1:3000:3000", "127.0.0.1:9090:9090", "127.0.0.1:3200:3200", "127.0.0.1:3100:3100", "127.0.0.1:4318:4318" })
                Contains(compose, published, "The card port must match a published compose port: " + published + ".");
            Contains(card, "Username: 'admin', Password: 'admin'", "The card must show Grafana's default credentials.");
            Contains(card, "<ClipboardButton", "Service URLs must be copyable with the shared copy control.");
            return Task.CompletedTask;
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string caseId, string displayName, Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor("Telemetry", caseId, displayName, executeAsync);
        }

        private static string ExpectedKind(Instrument instrument)
        {
            Type definition = instrument.GetType().IsGenericType ? instrument.GetType().GetGenericTypeDefinition() : instrument.GetType();
            if (definition == typeof(Counter<>)) return MetricKindEnum.Counter.ToString();
            if (definition == typeof(Histogram<>)) return MetricKindEnum.Histogram.ToString();
            if (definition == typeof(UpDownCounter<>)) return MetricKindEnum.UpDownCounter.ToString();
            if (definition == typeof(ObservableGauge<>)) return MetricKindEnum.ObservableGauge.ToString();
            return definition.Name;
        }

        private static void AssertLabelsDeclared(TelemetryCapture capture)
        {
            Dictionary<string, Convention> catalog = TelemetryCatalog.All.ToDictionary(c => c.Name, c => c, StringComparer.Ordinal);
            foreach (string instrument in catalog.Keys)
            {
                foreach (CapturedMeasurement measurement in capture.Find(instrument))
                {
                    if (measurement.Meter != TelemetryNames.MeterName) continue;
                    foreach (string key in measurement.Tags.Keys)
                        True(catalog[instrument].LabelKeys.Contains(key), "Undeclared label '" + key + "' recorded on " + instrument + ".");
                }
            }
        }

        private static async Task WithSampleDatabaseAsync(Func<DatabaseEntry, Task> action)
        {
            string path = Path.Combine(Path.GetTempPath(), "tablix_telemetry_sample_" + Guid.NewGuid().ToString("N") + ".db");
            File.Copy(FindSampleDatabase(), path, true);
            try
            {
                await action(new DatabaseEntry { Id = "telemetry_sqlite", Type = DatabaseTypeEnum.Sqlite, Filename = path, AllowedQueries = new List<string> { "SELECT" } }).ConfigureAwait(false);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                TryDelete(path);
            }
        }

        private static string FindRepositoryRoot()
        {
            return Path.GetDirectoryName(Path.GetDirectoryName(FindSampleDatabase()));
        }

        private static string FindSampleDatabase()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "docker", "database.db");
                if (File.Exists(candidate)) return candidate;
                directory = directory.Parent;
            }

            throw new FileNotFoundException("docker/database.db was not found above " + AppContext.BaseDirectory);
        }

        private static async Task<string> SendAsync(HttpClient http, HttpMethod method, string url, string body, string traceparent, int expectedStatus, CancellationToken token)
        {
            using HttpRequestMessage request = new HttpRequestMessage(method, url);
            if (body != null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            if (traceparent != null) request.Headers.TryAddWithoutValidation("traceparent", traceparent);
            using HttpResponseMessage response = await http.SendAsync(request, token).ConfigureAwait(false);
            string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Equal(expectedStatus, (int)response.StatusCode, method + " " + url + " returned " + content);
            return content;
        }

        private static async Task WaitForOkAsync(HttpClient http, string url, CancellationToken token)
        {
            for (int attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    using HttpResponseMessage response = await http.GetAsync(url, token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode) return;
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(100, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for " + url);
        }

        private static async Task<string> WaitForScrapeAsync(HttpClient http, string url, string expected, CancellationToken token)
        {
            string last = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    last = await http.GetStringAsync(url, token).ConfigureAwait(false);
                    if (last.Contains(expected, StringComparison.Ordinal)) return last;
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(200, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Prometheus scrape never contained " + expected + ". Last scrape: " + (last ?? "(none)"));
        }

        private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
        {
            for (int attempt = 0; attempt < 150; attempt++)
            {
                if (condition()) return;
                await Task.Delay(100, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Condition was not met within 15 seconds.");
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static void TryDelete(string filename)
        {
            try { if (File.Exists(filename)) File.Delete(filename); } catch (Exception) { }
        }

        private static void TryDeleteDirectory(string directory)
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (Exception) { }
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected: " + expected + ", actual: " + actual + ".");
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void False(bool condition, string message)
        {
            if (condition) throw new InvalidOperationException(message);
        }

        private static void Null(object value, string message)
        {
            if (value != null) throw new InvalidOperationException(message);
        }

        private static void NotNull(object value, string message)
        {
            if (value == null) throw new InvalidOperationException(message);
        }

        private static void Contains(string value, string expected, string message)
        {
            if (value == null || !value.Contains(expected, StringComparison.Ordinal)) throw new InvalidOperationException(message);
        }

        private static void DoesNotContain(string value, string unexpected, string message)
        {
            if (value != null && value.Contains(unexpected, StringComparison.Ordinal)) throw new InvalidOperationException(message);
        }

        #endregion
    }
}
