# Tablix Telemetry

Tablix ships observable. The server emits **metrics**, **traces**, and **logs** for every major code path, exports them through one telemetry host, and the Docker stack brings up an OpenTelemetry Collector, Prometheus, Tempo, Loki, and Grafana with eight provisioned dashboards. An on-call engineer can answer *where the time went* and *what failed* from dashboards and traces alone.

- [How it fits together](#how-it-fits-together)
- [Sources and names](#sources-and-names)
- [Enabling and configuring](#enabling-and-configuring)
- [Subscribing from another host](#subscribing-from-another-host)
- [Metrics catalog](#metrics-catalog)
- [Bounded label values](#bounded-label-values)
- [Built-in metrics from Watson, .NET, and the HTTP client](#built-in-metrics-from-watson-net-and-the-http-client)
- [Spans catalog](#spans-catalog)
- [Trace context propagation](#trace-context-propagation)
- [Logs](#logs)
- [The observability stack](#the-observability-stack)
- [Dashboard map](#dashboard-map)
- [Recommended alerts](#recommended-alerts)
- [Privacy and cardinality rules](#privacy-and-cardinality-rules)
- [Testing telemetry](#testing-telemetry)
- [Known limitations](#known-limitations)

## How it fits together

```
                 +--------------------------- tablix-server process ----------------------------+
 REST client --> | Watson (meter + source "Watson"): http.server.* metrics, one span per request |
 (traceparent)   |   +-- Tablix handlers (meter + source "Tablix"): chat, crawl, query, context    |
 MCP client  --> | Voltaic MCP (HttpListener): Tablix opens "tools/call {tool}" server spans      |
 (traceparent)   |   +-- persistence, user databases, model providers (client spans + metrics)   |
                 | Background: startup crawl, crawl after database add, provider health monitor |
                 |                                                                              |
                 | TelemetryHost (Radiant 0.1.2) subscribes to Tablix, Watson, System.Net.Http   |
                 +-------+-------------------------------+--------------------------------------+
                         | OTLP gRPC :4317 (traces, logs) | Prometheus scrape :9464/metrics
                         v                                v
                 OpenTelemetry Collector            Prometheus :9090
                   | traces       | logs                  |
                   v              v                       |
                 Tempo :3200    Loki :3100                |
                   \______________|_______________________/
                                  v
                            Grafana :3000  (folder "Tablix", 8 dashboards)
```

- **Emit rides the .NET base class library.** `Tablix.Core` creates one `Meter` and one `ActivitySource`, both named `Tablix`, and records through typed, bounded recorders (`TablixMetrics`, `TablixTracing`). It has no exporter or Radiant dependency, and every recorder is best-effort: with nothing listening the calls are near-free no-ops, and a telemetry failure never reaches request handling.
- **Watson measures HTTP.** Watson 7.2's built-in telemetry is explicitly enabled (`Settings.Telemetry.Enable`, `EnableMetrics`, `EnableTraces`, `PropagateContext`). It emits the REST request metrics and one server span per request; Tablix spans nest underneath it. Tablix does not duplicate them.
- **One host exports.** `Tablix.Server` starts a single `RadiantHost` (NuGet `Radiant` 0.1.2) at the composition root (`TelemetryHost`), subscribed to every meter and activity source in the process. It pushes traces and logs over OTLP, serves an in-process Prometheus endpoint, registers the metric catalog (`settings.Metrics.DefineAll`), adds .NET runtime metrics, and is disposed on shutdown. If it cannot start (for example the scrape port is taken), the server logs why and keeps running without export.

Radiant is used because Tablix is a service that needs exporting, a Prometheus endpoint, Loki-ready logs, and provider lifecycle handled correctly; it is not used in `Tablix.Core`.

## Sources and names

| Name | Kind | Owner | Contents |
| --- | --- | --- | --- |
| `Tablix` | Meter and ActivitySource | `Tablix.Core.Observability` | Every Tablix application metric and span below. |
| `Watson` | Meter and ActivitySource | Watson 7.2 | `http.server.*` and `watson.*` metrics, one server span per REST request. |
| `System.Net.Http` | Meter | .NET | `http.client.*` metrics for outbound model provider and health check calls. |
| `System.Runtime` | Meter | .NET (via Radiant runtime instrumentation) | `dotnet.*` GC, JIT, thread pool, CPU, memory, and exception metrics. |

All Tablix names (metric instruments, span names, attribute keys, and label values) live in one constants class, `src/Tablix.Core/Observability/TelemetryNames.cs`. The metric catalog with label sets and histogram buckets is `src/Tablix.Server/Observability/TelemetryCatalog.cs`. Treat these strings as public API: dashboards and alerts depend on them.

## Enabling and configuring

Telemetry is on by default. Settings live in the `Telemetry` section of `tablix.json` and apply at startup.

| Key | Default | Description |
| --- | --- | --- |
| `Enable` | `true` | Master switch for export. When `false`, nothing is exported; in-process emission stays near-free. |
| `ServiceName` | `tablix-server` | Stamped as `service.name` on every metric, span, and log. |
| `OtlpEnable` | `true` | Push traces, metrics, and logs over OTLP to `OtlpEndpoint`. Point it at a collector. |
| `OtlpEndpoint` | `http://127.0.0.1:4317` | OTLP endpoint. Invalid URIs reset to the default. |
| `OtlpProtocol` | `grpc` | `grpc` (port 4317) or `httpprotobuf` (port 4318). |
| `PrometheusEnable` | `true` | Serve an in-process Prometheus scrape endpoint on its own port. |
| `PrometheusHostname` | `127.0.0.1` | Bind name for the scrape endpoint. It answers only requests whose Host header matches, and wildcards (`*`, `+`, `0.0.0.0`) are rejected by the exporter. In a container, use the service name the scraper resolves (the Docker stack uses `tablix-server`). |
| `PrometheusPort` | `9464` | Scrape port (1 to 65535). |
| `PrometheusPath` | `/metrics` | Scrape path. |
| `LokiEnable` | `false` | Also push logs straight to a Loki 3.x OTLP endpoint, for deployments without a collector. Leave off when the collector forwards logs, or every line arrives twice. |
| `LokiEndpoint` | `http://127.0.0.1:3100/otlp` | Loki OTLP base endpoint (`/v1/logs` is appended). |
| `ExportLogs` | `true` | Forward Tablix log messages into the telemetry log pipeline, stamped with trace and span ids. |
| `TraceSamplingRatio` | `1.0` | Parent-based sampling ratio for new root traces (0.0 to 1.0). |
| `MetricExportIntervalMs` | `15000` | OTLP metric push interval (1000 to 300000). |

The Docker configuration (`docker/tablix.json`) exports to `http://otel-collector:4317` and binds the scrape endpoint to `tablix-server:9464`. Running from source with defaults, start the stack (or any collector on `127.0.0.1:4317`) and point Prometheus at `127.0.0.1:9464/metrics`. To turn export off entirely, set `"Telemetry": { "Enable": false }`.

Startup logs one line describing the outcome, for example `telemetry export enabled, Prometheus metrics at http://tablix-server:9464/metrics`, or the reason export is disabled including the inner error.

## Subscribing from another host

`Tablix.Core` emits through the BCL only, so any host in the same process can collect it without Radiant. With Radiant:

```csharp
RadiantSettings settings = new RadiantSettings("my-host");
settings.Sources.AddMeter("Tablix");
settings.Sources.AddActivitySource("Tablix");
settings.Sources.AddMeter("Watson");           // when hosting Watson
settings.Sources.AddActivitySource("Watson");
using (RadiantHost host = RadiantHost.Start(settings)) { /* run */ }
```

With the OpenTelemetry SDK directly: `.AddMeter("Tablix")` on a `MeterProviderBuilder` and `.AddSource("Tablix")` on a `TracerProviderBuilder`. With no SDK at all, a `System.Diagnostics.Metrics.MeterListener` and an `ActivityListener` work, which is how the tests assert on telemetry.

## Metrics catalog

Prometheus names apply the exporter's usual rewrite: dots become underscores, seconds histograms gain `_seconds`, counters gain `_total`, and label keys swap dots for underscores (`db.system.name` becomes `db_system_name`). Histograms carry explicit buckets (seconds): fast (100 microseconds to 2.5 s) for persistence, default (5 ms to 30 s) for requests and database operations, and slow (50 ms to 10 min) for crawl, chat, and model calls. Derive quantiles in Grafana with `histogram_quantile`; nothing is precomputed in-process.

| Instrument | Prometheus series | Type | Unit | Labels | Description |
| --- | --- | --- | --- | --- | --- |
| `tablix.crawl.jobs` | `tablix_crawl_jobs_total` | Counter | `{job}` | `trigger`, `db.system.name`, `outcome` | Schema crawl jobs by trigger, database system, and outcome. |
| `tablix.crawl.duration` | `tablix_crawl_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `trigger`, `db.system.name`, `outcome` | End-to-end schema crawl job duration. |
| `tablix.crawl.stage.duration` | `tablix_crawl_stage_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `stage`, `db.system.name`, `outcome` | Duration of each crawl pipeline stage, including queued. |
| `tablix.crawl.stage.events` | `tablix_crawl_stage_events_total` | Counter | `{event}` | `stage`, `db.system.name`, `outcome` | Crawl pipeline stage completions by stage and outcome. |
| `tablix.crawl.last_success` | `tablix_crawl_last_success_seconds` | ObservableGauge | `s` | `db.system.name` | Unix time of the last successful crawl, by database system. |
| `tablix.crawl.tables` | `tablix_crawl_tables_total` | Counter | `{table}` | `db.system.name` | Tables discovered by successful crawls. |
| `tablix.crawl.active` | `tablix_crawl_active` | UpDownCounter | `{job}` | none | Crawl jobs currently running. |
| `tablix.crawl_cache.lookups` | `tablix_crawl_cache_lookups_total` | Counter | `{lookup}` | `result` | Crawl cache lookups by result (hit or miss). |
| `tablix.crawl_cache.entries` | `tablix_crawl_cache_entries` | ObservableGauge | `{entry}` | `state` | Crawl cache entries by state (crawled or degraded). |
| `tablix.db.client.operations` | `tablix_db_client_operations_total` | Counter | `{operation}` | `db.system.name`, `db.operation.name`, `outcome` | Operations against user-configured databases by system, operation, and outcome. |
| `tablix.db.client.operation.duration` | `tablix_db_client_operation_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `db.system.name`, `db.operation.name`, `outcome` | Duration of operations against user-configured databases. |
| `tablix.db.client.active` | `tablix_db_client_active` | UpDownCounter | `{operation}` | `db.system.name` | Operations against user-configured databases currently in flight. |
| `tablix.query.executions` | `tablix_query_executions_total` | Counter | `{query}` | `source`, `statement`, `outcome` | SQL query executions by source, statement type, and outcome (including rejected). |
| `tablix.query.duration` | `tablix_query_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `source`, `statement`, `outcome` | SQL query execution duration including validation. |
| `tablix.query.rows` | `tablix_query_rows_bucket/_sum/_count` | Histogram | `{row}` | `source`, `statement` | Rows returned by successful queries. |
| `tablix.query.schema_refreshes` | `tablix_query_schema_refreshes_total` | Counter | `{refresh}` | `outcome` | Schema refreshes triggered by schema-related chat query failures. |
| `tablix.chat.requests` | `tablix_chat_requests_total` | Counter | `{request}` | `mode`, `execution_path`, `outcome` | Chat requests by mode, execution path, and outcome. |
| `tablix.chat.duration` | `tablix_chat_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `mode`, `outcome` | End-to-end chat request duration. |
| `tablix.chat.stage.duration` | `tablix_chat_stage_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `stage`, `outcome` | Duration of each chat workflow stage. |
| `tablix.chat.tool_calls` | `tablix_chat_tool_calls_total` | Counter | `{call}` | `gen_ai.tool.name`, `phase`, `outcome` | Chat tool calls by tool, phase, and outcome. |
| `tablix.chat.tool_call.duration` | `tablix_chat_tool_call_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `gen_ai.tool.name`, `outcome` | Chat tool call duration. |
| `tablix.chat.time_to_first_token` | `tablix_chat_time_to_first_token_seconds_bucket/_sum/_count` | Histogram | `s` | `gen_ai.provider.name` | Time to first streamed token from a model provider. |
| `tablix.context.builds` | `tablix_context_builds_total` | Counter | `{build}` | `scope`, `outcome` | Model-generated context builds by scope and outcome. |
| `tablix.context.build.duration` | `tablix_context_build_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `scope`, `outcome` | Model-generated context build duration. |
| `tablix.context.build.stage.duration` | `tablix_context_build_stage_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `scope`, `stage`, `outcome` | Duration of each context build stage (queued, inference, persist). |
| `tablix.context.build.slots.in_use` | `tablix_context_build_slots_in_use` | UpDownCounter | `{slot}` | none | Context build concurrency slots currently held. |
| `tablix.context.updates` | `tablix_context_updates_total` | Counter | `{update}` | `scope`, `source`, `outcome` | Context writes by scope, source, and outcome. |
| `tablix.model.requests` | `tablix_model_requests_total` | Counter | `{request}` | `gen_ai.provider.name`, `gen_ai.operation.name`, `outcome` | Model provider requests by provider, operation, and outcome. |
| `tablix.model.request.duration` | `tablix_model_request_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `gen_ai.provider.name`, `gen_ai.operation.name`, `outcome` | Model provider request duration. |
| `tablix.model.tokens` | `tablix_model_tokens_total` | Counter | `{token}` | `gen_ai.provider.name`, `gen_ai.token.type` | Model tokens by provider and token type. |
| `tablix.model.health.transitions` | `tablix_model_health_transitions_total` | Counter | `{transition}` | `gen_ai.provider.name`, `state` | Model provider health state transitions. |
| `tablix.model.health.last_cycle` | `tablix_model_health_last_cycle_seconds` | ObservableGauge | `s` | none | Unix time the health monitor last completed a scheduling cycle. |
| `tablix.model.providers` | `tablix_model_providers` | ObservableGauge | `{provider}` | `state` | Configured model providers by health state. |
| `tablix.mcp.tool.calls` | `tablix_mcp_tool_calls_total` | Counter | `{call}` | `gen_ai.tool.name`, `outcome` | MCP tool calls by tool and outcome. |
| `tablix.mcp.tool.duration` | `tablix_mcp_tool_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `gen_ai.tool.name`, `outcome` | MCP tool call duration. |
| `tablix.mcp.tool.active` | `tablix_mcp_tool_active` | UpDownCounter | `{call}` | none | MCP tool calls currently in flight. |
| `tablix.persistence.operations` | `tablix_persistence_operations_total` | Counter | `{operation}` | `db.operation.name`, `outcome` | Tablix persistence (internal SQLite store) operations by kind and outcome. |
| `tablix.persistence.operation.duration` | `tablix_persistence_operation_duration_seconds_bucket/_sum/_count` | Histogram | `s` | `db.operation.name`, `outcome` | Persistence operation duration, excluding the lock wait. |
| `tablix.persistence.lock.wait` | `tablix_persistence_lock_wait_seconds_bucket/_sum/_count` | Histogram | `s` | `db.operation.name` | Time spent waiting for the single persistence operation slot. |
| `tablix.persistence.lock.waiting` | `tablix_persistence_lock_waiting` | UpDownCounter | `{operation}` | none | Persistence operations waiting for the operation slot. |
| `tablix.errors` | `tablix_errors_total` | Counter | `{error}` | `component`, `error.type` | Handled errors by component and error type. |
| `tablix.settings.updates` | `tablix_settings_updates_total` | Counter | `{update}` | `outcome` | Settings updates by outcome. |
| `tablix.build.info` | `tablix_build_info` | ObservableGauge | `{info}` | `version`, `runtime` | Build information (always 1), labeled by version and runtime. |
| `tablix.config.flag` | `tablix_config_flag` | ObservableGauge | `{flag}` | `flag` | Safe boolean configuration flags (1 enabled, 0 disabled). |

## Bounded label values

Every metric label takes values from a fixed set. Identifiers and free text (database ids, provider ids, table names, model names, query text) are span attributes only.

| Label | Values |
| --- | --- |
| `outcome` | `success`, `failure` (handled failure such as a provider error or a query the database refused), `error` (exception), `rejected` (refused by validation or policy before running), `canceled` |
| `trigger` | `startup`, `api`, `api_stream`, `chat`, `query_retry`, `mcp`, `database_added`, `on_demand` |
| `stage` (crawl) | `queued` (batch position), `discover`, `examine`, `cache` |
| `stage` (chat) | `prepare`, `tool_selection`, `planner`, `tool`, `final_inference` |
| `stage` (context build) | `queued` (provider concurrency slot), `inference`, `persist` |
| `db.system.name` | `sqlite`, `postgresql`, `mysql`, `microsoft.sql_server` |
| `db.operation.name` | `crawl`, `query`, `test_connection` (user databases); `read`, `write` (persistence) |
| `gen_ai.provider.name` | `openai`, `openai_compatible`, `gemini`, `ollama` |
| `gen_ai.operation.name` | `chat`, `chat_stream`, `tool_chat`, `tool_chat_stream`, `health_check`, `connectivity_test` |
| `gen_ai.token.type` | `input`, `output` |
| `gen_ai.tool.name` | The 13 registered MCP tools (`tablix_discover_databases`, ...) or the three chat tools; anything else is `unknown` |
| `mode` | `sync`, `stream` |
| `execution_path` | Server-chosen chat path, for example `native_tool_calls`, `native_no_tool_call`, `server_fallback`, `fallback_no_plan`, `plain`, `execution_disabled`, `native_tool_call_failed`, `fallback_planner_failed` |
| `phase` | `native`, `fallback` |
| `source` (query) | `rest`, `mcp`, `chat` |
| `source` (context write) | `user`, `mcp`, `chat`, `model`, `other` |
| `statement` | `select`, `insert`, `update`, `delete`, `merge`, `create`, `alter`, `drop`, `truncate`, `replace`, `pragma`, `explain`, `show`, `describe`, `exec`, `call`, `other` |
| `scope` | `database`, `table` |
| `state` | `crawled`, `degraded` (crawl cache); `healthy`, `unhealthy`, `unmonitored` (providers) |
| `result` | `hit`, `miss` |
| `component` | `rest`, `mcp`, `chat`, `crawl`, `context_build`, `health_check`, `persistence`, `lifecycle` |
| `error.type` | Exception type name (for example `SqliteException`) |
| `flag` | `chat_enabled`, `chat_default_streaming`, `chat_context_updates`, `prompt_retry_after_schema_refresh`, `rest_ssl`, `telemetry_otlp`, `telemetry_prometheus`, `telemetry_loki` |

## Built-in metrics from Watson, .NET, and the HTTP client

| Family | Source | Use |
| --- | --- | --- |
| `http_server_request_duration_seconds` (labels `http_request_method`, `http_route`, `http_response_status_code`) | Watson | REST rate, errors, and latency by route template. |
| `http_server_active_requests`, `http_server_request_body_size_bytes`, `http_server_response_body_size_bytes` | Watson | Concurrency and payload sizes. |
| `watson_server_up`, `watson_server_uptime_seconds`, `watson_server_connections_*`, `watson_server_requests_aborted_total`, `watson_server_requests_disconnected_total`, `watson_auth_requests_total`, `watson_route_matches_total`, `watson_server_received_bytes_total`, `watson_server_sent_bytes_total` | Watson | Liveness, connections, auth decisions, routing. |
| `http_client_request_duration_seconds` (labels `server_address`, `error_type`, ...) | `System.Net.Http` | Outbound calls to model providers and health check URLs, including DNS and connection failures. |
| `dotnet_gc_*`, `dotnet_process_*`, `dotnet_thread_pool_*`, `dotnet_exceptions_total`, `dotnet_jit_*`, `dotnet_monitor_lock_contentions_total` | `System.Runtime` | Runtime health. |

Watson records `http.server.request.duration` in seconds without bucket advice, so the SDK default boundaries (5, 10, ... 10000, intended for milliseconds) would put every request in the first bucket. `TelemetryHost` registers a seconds-scale view (5 ms to 30 s) for that instrument.

## Spans catalog

All Tablix spans come from the `Tablix` activity source. Status is set explicitly (`Ok` or `Error`); failures carry `error.type` and, for exceptions, the OpenTelemetry `exception` event. Policy rejections are tagged `outcome=rejected` without an error status.

| Span name | Kind | Parent | Key attributes | Emitted by |
| --- | --- | --- | --- | --- |
| `{METHOD} {route}` | Server | Caller's `traceparent`, else root | `http.*` (Watson) | Watson, one per REST request |
| `tablix.startup` | Internal | Root | `outcome` | Server start; children `stage:persistence`, `stage:logging`, `stage:crawl_cache`, `stage:health_checks`, `stage:rest`, `stage:mcp` |
| `crawl.batch` | Internal | Root, linked to the trace that scheduled it | `trigger` | Startup crawl and the background crawl after a database is added |
| `crawl.job` | Internal | Request span or `crawl.batch` | `tablix.database.id`, `db.system.name`, `trigger`, `tablix.table.count` | Every crawl; children `stage:queued`, `stage:discover`, `stage:examine`, `stage:cache`, and the `{db.system.name} crawl` client span |
| `{db.system.name} crawl` / `query` / `test_connection` | Client | Current span | `db.system.name`, `db.operation.name`, `db.namespace`, `server.address`, `server.port`, `statement`, `tablix.row.count` | Every operation against a user-configured database (`InstrumentedDatabaseCrawler`) |
| `tablix.persistence read` / `write` | Client | Current span | `db.system.name=sqlite`, `db.operation.name`, `code.function.name` | Every operation on the Tablix state store, including the lock wait |
| `chat` | Internal | Watson request span | `mode`, `execution_path`, `tablix.database.id`, `tablix.provider.id`, `gen_ai.provider.name`, `gen_ai.request.model` | `POST /v1/chat` and `/v1/chat/stream` |
| `stage:prepare`, `stage:tool_selection`, `stage:planner`, `stage:tool`, `stage:final_inference` | Internal | `chat` | `outcome` | Chat workflow stages |
| `execute_tool {tool}` | Internal | `stage:tool` | `gen_ai.tool.name`, `phase` | Each chat tool call |
| `context.build` | Internal | Watson request span | `scope`, `tablix.database.id` | Database and table context generation. Database scope has children `stage:inference` and `stage:persist`; table scope has one `context.table` child per table |
| `context.table` | Internal | `context.build` | `tablix.table.id`, `tablix.database.id` | One table of a table context build; children `stage:queued` (provider concurrency slot), `stage:inference`, `stage:persist` |
| `{gen_ai.provider.name} {gen_ai.operation.name}` | Client | Current stage, or root for health checks | `gen_ai.*` (provider, operation, request and response model, input and output tokens), `server.address`, `server.port`, `http.response.status_code`, `tablix.provider.id` | Every model provider call: chat, tool chat, streaming, connectivity tests, background health checks |
| `tools/call {tool}` | Server | Caller's `traceparent`, else root | `mcp.method.name`, `gen_ai.tool.name`, `gen_ai.operation.name=execute_tool` | Every MCP tool call |

## Trace context propagation

- **Inbound REST:** Watson adopts an inbound W3C `traceparent` (`PropagateContext = true`); Tablix spans nest under Watson's request span.
- **Inbound MCP:** Voltaic's MCP transport exposes the HTTP request only to its authentication hook. Tablix installs a hook that always admits the request (MCP stays unauthenticated, as before) and copies `traceparent` and `tracestate` into the call-context claims; the tool handler parents its `tools/call` span on them.
- **Outbound HTTP:** model provider calls and health checks go through `HttpClient`, which injects `traceparent` from the current span.
- **Background hand-offs:** the startup crawl, the crawl after a database is added, and each provider health check start their own root traces (linked to the scheduling trace where one exists), so long-running background work never inflates or joins a request trace. The startup trace is detached before listeners start, so requests never join it.

## Logs

Tablix logs through SyslogLogging. `TelemetryHost` subscribes to `LoggingModule.MessageLogged` and forwards each entry to a Radiant `ILogger`, which stamps the active `trace_id` and `span_id` and exports over OTLP (to Loki through the collector, or directly when `LokiEnable` is set). Logs are added because Tablix does background work (startup crawl, background crawls, provider health monitor). Existing console, file, and syslog destinations are unchanged.

In Grafana, Loki's `TraceID` derived field links a log line to its trace, and Tempo's trace-to-logs link opens the logs for a span.

## The observability stack

`docker/compose.yaml` brings everything up with one command:

```bash
cd docker
docker compose up -d
```

| Service | Image | Host URL | Credentials | Role |
| --- | --- | --- | --- | --- |
| Grafana | `grafana/grafana-oss:13.0.2` | http://localhost:3000 | `admin` / `admin` (local default) | Datasources and the `Tablix` dashboard folder |
| Prometheus | `prom/prometheus:v3.14.0` | http://localhost:9090 | none | Scrapes `tablix-server:9464/metrics` every 15 s |
| Tempo | `grafana/tempo:2.6.1` | http://localhost:3200 | none | Trace store (OTLP from the collector) |
| Loki | `grafana/loki:3.3.0` | http://localhost:3100 | none | Log store (OTLP from the collector) |
| OpenTelemetry Collector | `otel/opentelemetry-collector-contrib:0.109.0` | OTLP `localhost:4317` (gRPC), `localhost:4318` (HTTP) | none | Routes traces to Tempo and logs to Loki; drops OTLP metrics (Prometheus scrapes them) |

- Observability ports are published on host loopback (`127.0.0.1`) only. Prometheus, Tempo, Loki, the collector, and the `/metrics` endpoint have no authentication; keep them on an internal network. The `/metrics` port is not published at all.
- Healthchecks run every 5 s with 2 retries against `127.0.0.1` (curl for Tablix and Grafana; the Prometheus, Tempo, and Loki images ship `wget` instead). Grafana starts only after Prometheus, Tempo, and Loki are healthy; Prometheus waits for a healthy `tablix-server`; the collector waits for healthy Tempo and Loki, and `tablix-server` starts after the collector.
- Prometheus scrapes the classic text format (`scrape_protocols: ['PrometheusText0.0.4']`). With OpenMetrics negotiation, Prometheus 3 receives dotted UTF-8 names and rejects the whole scrape because a `# UNIT` is not a suffix of the dotted name.
- Grafana is provisioned as code: datasources with stable UIDs (`prometheus`, `tempo`, `loki`) in `docker/grafana/provisioning/datasources/`, and a file provider that loads `assets/grafana/*.json` into the `Tablix` folder.
- **Production:** change Grafana's credentials before sharing the stack. Set `GRAFANA_ADMIN_PASSWORD` (and optionally `GRAFANA_ADMIN_USER`) in an untracked `.env` file or the environment; never commit a real password. Sign-up is disabled.

The dashboard home page (Databases) carries an **External Services** card, low on the page, that lists each service with its browser-reachable URL, a copy button, default credentials, and a live reachability badge.

## Dashboard map

All dashboards are tagged `tablix` and link to each other.

| Dashboard | UID | Answers |
| --- | --- | --- |
| Tablix / Overview | `tablix-overview` | Is it up, how busy, what is failing, which domain is slow, process memory and CPU, configuration flags. Start here. |
| Tablix / HTTP | `tablix-http` | REST rate by route and status, p50/p95/p99, top routes by p95, connections, aborts, auth decisions (Watson). |
| Tablix / Chat & Context | `tablix-chat` | Chat outcomes, execution paths, p95 per stage, time to first token, tool calls, context builds with queued/inference/persist stages, context writes by source. |
| Tablix / Crawl Pipeline | `tablix-crawl` | Crawl jobs by trigger and outcome, per-stage p95 and failures, time since last success, degraded cache entries, cache hit ratio. |
| Tablix / Queries & Databases | `tablix-queries` | Queries by source, statement, and outcome (including rejections), latency, rows, schema-refresh retries, user-database operations by system. |
| Tablix / Integrations | `tablix-integrations` | Model providers (requests, p95, tokens, health state and transitions, outbound HTTP errors), MCP tools, persistence operations and lock contention. |
| Tablix / Runtime | `tablix-runtime` | GC, allocation, exceptions, CPU, thread pool, lock contention. |
| Tablix / Logs & Traces | `tablix-logs-traces` | Recent error traces, slow traces, crawl jobs, model calls (TraceQL), warnings and errors, and all logs (Loki). |

## Recommended alerts

```yaml
groups:
  - name: tablix
    rules:
      - alert: TablixDown
        expr: up{job="tablix-server"} == 0 or absent(watson_server_up)
        for: 1m
      - alert: TablixHttp5xxRatioHigh
        expr: sum(rate(http_server_request_duration_seconds_count{http_response_status_code=~"5.."}[5m])) / clamp_min(sum(rate(http_server_request_duration_seconds_count[5m])), 1e-9) > 0.05
        for: 5m
      - alert: TablixChatFailing
        expr: sum(rate(tablix_chat_requests_total{outcome=~"failure|error"}[10m])) / clamp_min(sum(rate(tablix_chat_requests_total[10m])), 1e-9) > 0.2
        for: 10m
      - alert: TablixChatSlow
        expr: histogram_quantile(0.95, sum by (le) (rate(tablix_chat_duration_seconds_bucket[10m]))) > 60
        for: 10m
      - alert: TablixModelProviderUnhealthy
        expr: sum(tablix_model_providers{state="unhealthy"}) > 0
        for: 5m
      - alert: TablixHealthMonitorStalled
        expr: time() - max(tablix_model_health_last_cycle_seconds) > 120
        for: 2m
      - alert: TablixCrawlFailures
        expr: sum(increase(tablix_crawl_jobs_total{outcome!="success"}[30m])) > 0
      - alert: TablixDegradedMetadata
        expr: sum(tablix_crawl_cache_entries{state="degraded"}) > 0
        for: 15m
      - alert: TablixPersistenceContention
        expr: histogram_quantile(0.95, sum by (le) (rate(tablix_persistence_lock_wait_seconds_bucket[5m]))) > 0.5
        for: 10m
      - alert: TablixPersistenceErrors
        expr: sum(rate(tablix_errors_total{component="persistence"}[5m])) > 0
        for: 5m
      - alert: TablixMcpToolErrors
        expr: sum(rate(tablix_mcp_tool_calls_total{outcome="error"}[10m])) > 0
        for: 10m
```

## Privacy and cardinality rules

- No secrets, credentials, prompts, completions, query text, or result rows are recorded on metrics, spans, or logs emitted by the telemetry layer. Provider error descriptions on spans have the provider API key redacted and are truncated to 256 characters.
- Metric labels are bounded (see above). Ids live on spans only. The test suite fails if any recorded label is missing from the catalog, if a dashboard queries a series Tablix does not emit, or if a secret or database id reaches the Prometheus scrape.
- Watson enforces route templates (`/v1/database/{id}`) for HTTP labels.

## Testing telemetry

`src/Test.Shared/TablixTelemetrySuites.cs` runs in all three runners (console, xUnit, NUnit). It attaches an in-memory `MeterListener` and `ActivityListener` (`TelemetryCapture`) and covers: the no-listener path; catalog and instrument parity; naming and unit conventions; settings defaults and clamping; telemetry host subscription, disabled export, and start failure; crawl success and failure with every stage; crawl cache hits, misses, and gauges; user-database query, failure, and connectivity spans; persistence operations, lock wait, rejections, and context writes; model call outcomes, tokens, time to first token, and secret redaction; MCP tool spans that continue a caller trace; the background health monitor (root spans, transitions, gauges, heartbeat); an end-to-end server run that drives REST, chat (success and provider failure) against a fake OpenAI-compatible provider, and MCP, then scrapes the real Prometheus endpoint and checks span parentage and outbound `traceparent`; dashboard queries against the catalog; and the compose, provisioning, and services-card contract.

## Known limitations

- The OpenTelemetry Collector image is distroless, so it has no healthcheck; `tablix-server` depends on it with `service_started`.
- Token metrics count only usage the provider reports. Streaming responses that never send usage contribute none.
- Time to first token is recorded for streamed responses only.
- The Tablix MCP server is unauthenticated (unchanged); the trace-context hook admits every request exactly as before.
