# Tablix SQLite Persistence Plan

Tablix v0.2.0 should stop treating `tablix.json` as the product database. The JSON file remains the bootstrap and runtime settings file for server concerns such as ports, logging, authentication keys, and the persistence database location. Product state moves into a single SQLite file: configured databases, model providers, crawled schema metadata, database-level context, table-level context, setup wizard state, and related records added later.

The implementation should land as a whole-product update. Backend persistence, REST APIs, MCP behavior, dashboard workflows, Docker packaging, Postman examples, tests, and documentation must agree on the same model. The user experience target is simple: `docker compose pull`, `docker compose up`, sign into the dashboard, complete the setup wizard, then chat with a configured database.

## Implementation Status

- [x] Added SQLite persistence settings, enum, driver base/factory, SQLite driver, migrations, seed data, method interfaces, and typed models.
- [x] Moved default providers and configured databases out of default `tablix.json` templates and into seeded `tablix.db`.
- [x] Added persistence initialization, legacy JSON import, and default seeding at server startup.
- [x] Updated Database, Chat, Settings, and MCP paths to read/write persisted provider, database, metadata, and context records.
- [x] Added Models REST API, Setup REST API, provider/database validation APIs, and table-context APIs using existing `/v1/...` route convention.
- [x] Added dashboard Models page, first-run setup wizard, Settings persistence section, saved database test action, and table-context editors.
- [x] Updated Docker compose mounts, generated `docker/tablix.db` and `docker/factory/tablix.db`, and updated factory reset scripts.
- [x] Updated README, GETTING_STARTED, REST_API, MCP_API, CHANGELOG, and Postman collection for the SQLite persistence model.
- [x] Updated Touchstone shared tests and dashboard contract tests.
- [x] Made `context_records` the source of truth for database-scope and table-scope context; the legacy `database_connections.context` column is retained only for schema compatibility.
- [x] Added model-generated table context through REST, dashboard detail page actions, first-run setup wizard, docs, Postman, and contract tests.
- [x] Verified `dotnet build src\Tablix.slnx`, `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj`, `dotnet test` for xUnit/NUnit adapters, `npm.cmd run build`, JSON validation, SQLite seed validation, Docker compose config validation, and C# structural scans after the latest edits.
- [ ] Human/manual Docker smoke test remains: `docker compose pull`, `docker compose up`, login, complete wizard, validate provider/database, crawl, build contexts, and chat with the database.
- [ ] Human/provider validation remains for real external model endpoints requiring credentials.

## Non-Blocking Assumptions

- [x] Treat SQLite as the only supported Tablix persistence database type in v0.2.0.
- [x] Add a new persistence enum, for example `TablixPersistenceDatabaseTypeEnum`, with only `Sqlite`.
- [x] Keep the existing target database enum `DatabaseTypeEnum` for user-configured databases; that enum still describes SQLite, MySQL, PostgreSQL, and SQL Server databases that Tablix can inspect.
- [x] Keep `tablix.json` for server/bootstrap settings only.
- [x] Move `TablixSettings.Databases` and `ChatSettings.Providers` out of settings classes and JSON defaults.
- [x] Keep the existing Tablix REST route convention: version prefix first, then singular resource groups such as `/v1/database`, `/v1/chat`, and `/v1/settings`.
- [x] Do not add route aliases or duplicate REST paths.
- [x] Store secrets in SQLite for v0.2.0, but never return plaintext credentials from read APIs, MCP tools, logs, request history, or chat/tool telemetry.
- [x] Do not enable SQLite write-ahead logging. Force rollback journal mode so `tablix.db` remains a single-file database.
- [x] No direct ORM dependency is planned. Use `Microsoft.Data.Sqlite`, handwritten SQL query classes, typed models, and domain-specific method interfaces.
- [x] The first-run wizard appears when persistent state has no completed setup marker or when no enabled provider/database/crawled schema exists.

## Requirements Guardrails

- [x] Namespace declarations remain at the top of every C# file, with `using` statements inside the namespace.
- [x] System usings come first and are alphabetized, followed by project/third-party usings alphabetized.
- [x] No `var` local declarations.
- [x] No tuples or tuple deconstruction; use named request/result classes.
- [x] No `partial` classes.
- [x] No nested classes or multiple classes/enums/interfaces/delegates/structs/records in one file.
- [x] No direct `System.Text.Json` DOM types such as `JsonElement`, `JsonNode`, `JsonObject`, `JsonArray`, or `JsonDocument`.
- [x] Public classes, interfaces, enum members, constructors, properties, and methods have XML documentation.
- [x] Async methods accept `CancellationToken` unless the type owns an appropriate cancellation source.
- [x] Awaited calls in library/server code use `.ConfigureAwait(false)` where appropriate.
- [x] Methods returning `IEnumerable` also expose async variants with cancellation where appropriate.
- [x] Disposable database drivers implement `IDisposable` and `IAsyncDisposable`.
- [x] SQLite write operations are concurrency controlled.
- [x] SQL is parameterized for values and kept in provider-specific query classes where practical.
- [x] REST handlers use typed DTOs and set status codes explicitly.
- [x] Route handlers do not return tuples.
- [x] Touchstone shared tests remain the single source of truth for automated, xUnit, and NUnit runners.

## Target Architecture

The persistence layer should be a provider-neutral boundary with a SQLite implementation. It should follow the required implementation interface property pattern: a base driver exposes domain-specific method interfaces through protected-set public properties, and the SQLite driver initializes those properties with concrete method implementations.

Suggested backend layout:

```text
src/
  Tablix.Core/
    Enums/
      TablixPersistenceDatabaseTypeEnum.cs
      SetupWizardStatusEnum.cs
      ContextScopeEnum.cs
    Models/
      DatabaseConnectionCreateRequest.cs
      DatabaseConnectionUpdateRequest.cs
      DatabaseConnectionRead.cs
      DatabaseConnectionSummary.cs
      ModelProviderCreateRequest.cs
      ModelProviderUpdateRequest.cs
      ModelProviderRead.cs
      ModelProviderSummary.cs
      PromptIntentRead.cs
      PromptProcessingResult.cs
      PromptQueryPlan.cs
      TableMetadataRead.cs
      TableContextRead.cs
      TableContextUpdateRequest.cs
      SetupStateRead.cs
      SetupStateUpdateRequest.cs
      ProviderConnectivityTestRequest.cs
      ProviderConnectivityTestResponse.cs
      DatabaseConnectivityTestRequest.cs
      DatabaseConnectivityTestResponse.cs
      PersistenceHealthRead.cs
    Settings/
      PersistenceDatabaseSettings.cs
      TablixSettings.cs
    Persistence/
      DatabaseDriverBase.cs
      DatabaseDriverFactory.cs
      SchemaMigration.cs
      Interfaces/
        IModelProviderMethods.cs
        IDatabaseConnectionMethods.cs
        IDatabaseMetadataMethods.cs
        IDatabaseContextMethods.cs
        ITableContextMethods.cs
        ISetupStateMethods.cs
        ISettingsStateMethods.cs
      Sqlite/
        SqliteDatabaseDriver.cs
        Sanitizer.cs
        Converters.cs
        Implementations/
          SqliteModelProviderMethods.cs
          SqliteDatabaseConnectionMethods.cs
          SqliteDatabaseMetadataMethods.cs
          SqliteDatabaseContextMethods.cs
          SqliteTableContextMethods.cs
          SqliteSetupStateMethods.cs
          SqliteSettingsStateMethods.cs
        Queries/
          SqliteSchemaQueries.cs
          SqliteModelProviderQueries.cs
          SqliteDatabaseConnectionQueries.cs
          SqliteDatabaseMetadataQueries.cs
          SqliteContextQueries.cs
          SqliteSetupStateQueries.cs
```

Server layout:

```text
src/
  Tablix.Server/
    Persistence/
      TablixPersistenceService.cs
      TablixPersistenceMigrationService.cs
      TablixPersistenceSeeder.cs
      JsonSettingsMigrationService.cs
    Routes/
      DatabaseRoutes.cs
      ModelProviderRoutes.cs
      ContextRoutes.cs
      MetadataRoutes.cs
      SetupRoutes.cs
      SettingsRoutes.cs
      ChatRoutes.cs
      HealthRoutes.cs
    Handlers/
      PromptIntentClassifier.cs
      NativeToolChatOrchestrator.cs
      FallbackQueryPlanner.cs
      ChatQueryExecutionService.cs
      ...
```

If the existing SwiftStack style makes route registrar classes more practical as static classes wrapping `RestApp`, use that pattern. Avoid growing `TablixServer.InitializeRest()` into a larger monolith.

## Settings Changes

`tablix.json` should hold only server/bootstrap settings and should be small enough to inspect safely.

- [x] Add `Persistence` or `Database` settings object to `TablixSettings`.
- [x] Use a name that does not conflict with configured target databases. Recommended: `Persistence`.
- [x] Include `Type` with enum value `Sqlite`.
- [x] Include `Filename` defaulting to `tablix.db`.
- [x] Resolve relative filenames relative to the settings file directory, not the current working directory.
- [x] Remove `Databases` from `TablixSettings`.
- [x] Remove `Providers` from `ChatSettings`.
- [x] Keep `Chat.DefaultProviderId`, `Chat.DefaultStreaming`, `Chat.SystemPrompt`, `Chat.MaxContextTables`, `Chat.Tools`, and `Chat.PromptProcessing` in JSON unless a later design explicitly moves them.
- [x] Keep `ApiKeys`, `Rest`, and `Logging` in JSON.
- [x] Update `SettingsReadResponse` to show persistence database type and redacted/resolved filename metadata.
- [x] Update `SettingsUpdateRequest` so persistence filename/type changes are either blocked at runtime or clearly marked restart-required.
- [x] Update settings UI to annotate persistence filename/type as restart-required.
- [x] Ensure default `docker/tablix.json`, `docker/factory/tablix.json`, and `src/Tablix.Server/tablix.json` include the new persistence object.

Expected JSON shape:

```json
{
  "Persistence": {
    "Type": "Sqlite",
    "Filename": "tablix.db"
  },
  "Rest": {},
  "Logging": {},
  "ApiKeys": [],
  "Chat": {}
}
```

## SQLite Schema

Use snake_case table and column names inside SQLite. Preserve PascalCase JSON contracts at the API boundary through typed DTOs and the centralized serializer.

### Schema Migrations

- [ ] Create `schema_migrations`.
- [ ] Track integer version, description, applied UTC timestamp, and checksum/hash if practical.
- [ ] Run migrations during persistence driver initialization.
- [ ] Make migrations idempotent and safe to rerun.
- [ ] Keep migration statements in `SqliteSchemaQueries`.
- [ ] Add tests proving repeated initialization does not duplicate seed data or fail migrations.

Suggested `schema_migrations` columns:

```text
version INTEGER PRIMARY KEY
description TEXT NOT NULL
applied_utc TEXT NOT NULL
checksum TEXT NULL
```

### Model Providers

- [ ] Create `model_providers`.
- [ ] Store all current provider fields now found in `ModelProviderSettings`.
- [ ] Store API keys but never return them from read APIs.
- [ ] Include created/updated timestamps.
- [ ] Include enabled and default streaming flags.
- [ ] Include native tool capability fields.
- [ ] Enforce unique provider ID.
- [ ] Add indexes for enabled providers and name search.

Suggested columns:

```text
id TEXT PRIMARY KEY
name TEXT NOT NULL
type TEXT NOT NULL
endpoint TEXT NOT NULL
api_key TEXT NULL
model TEXT NOT NULL
system_prompt TEXT NULL
enabled INTEGER NOT NULL
default_streaming INTEGER NOT NULL
supports_native_tool_calls INTEGER NOT NULL
use_native_tool_calls INTEGER NOT NULL
supports_strict_json INTEGER NOT NULL
tool_capability_note TEXT NULL
temperature REAL NOT NULL
top_p REAL NULL
max_tokens INTEGER NOT NULL
request_timeout_ms INTEGER NOT NULL
created_utc TEXT NOT NULL
updated_utc TEXT NOT NULL
```

### Configured Databases

- [ ] Create `database_connections`.
- [ ] Store all current `DatabaseEntry` fields.
- [ ] Preserve allowed query operations as a child table rather than CSV or raw JSON.
- [ ] Store credentials but never return plaintext user/password from read APIs.
- [ ] Include database-level context as persistent editable text.
- [ ] Include created/updated timestamps.
- [ ] Add indexes on type, name, database name, and enabled/active status if added.

Suggested columns:

```text
id TEXT PRIMARY KEY
name TEXT NOT NULL
type TEXT NOT NULL
hostname TEXT NULL
port INTEGER NULL
database_name TEXT NULL
schema_name TEXT NULL
username TEXT NULL
password TEXT NULL
filename TEXT NULL
context TEXT NULL
created_utc TEXT NOT NULL
updated_utc TEXT NOT NULL
last_connection_test_utc TEXT NULL
last_connection_test_success INTEGER NULL
last_connection_test_message TEXT NULL
```

Suggested child table:

```text
database_allowed_queries
database_id TEXT NOT NULL
query_operation TEXT NOT NULL
PRIMARY KEY (database_id, query_operation)
FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE
```

### Crawled Database Metadata

- [ ] Persist crawl results currently held only in `CrawlCache`.
- [ ] Create `database_crawls` for crawl run summaries.
- [ ] Create `database_tables` for table-level metadata.
- [ ] Create `database_columns`.
- [ ] Create `database_indexes`.
- [ ] Create `database_index_columns`.
- [ ] Create `database_foreign_keys`.
- [ ] Create `database_foreign_key_columns` if composite keys are needed now or soon.
- [ ] Replace metadata rows transactionally at the end of a successful crawl.
- [ ] Store degraded crawl state and error messages without deleting the last good metadata unless a clear replacement policy is chosen.
- [ ] Expose last successful crawl and last attempted crawl separately.
- [ ] Keep enough metadata to rebuild `DatabaseDetail`, table list pages, relationship pages, chat context, and MCP discovery responses without re-crawling on every request.

Suggested `database_crawls` columns:

```text
id TEXT PRIMARY KEY
database_id TEXT NOT NULL
started_utc TEXT NOT NULL
completed_utc TEXT NULL
success INTEGER NOT NULL
table_count INTEGER NOT NULL
relationship_count INTEGER NOT NULL
error TEXT NULL
FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE
```

Suggested `database_tables` columns:

```text
id TEXT PRIMARY KEY
database_id TEXT NOT NULL
schema_name TEXT NULL
table_name TEXT NOT NULL
table_type TEXT NULL
row_count INTEGER NULL
description TEXT NULL
last_crawled_utc TEXT NOT NULL
FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE
UNIQUE (database_id, schema_name, table_name)
```

Suggested `database_columns` columns:

```text
id TEXT PRIMARY KEY
table_id TEXT NOT NULL
column_name TEXT NOT NULL
ordinal INTEGER NOT NULL
data_type TEXT NULL
is_nullable INTEGER NOT NULL
is_primary_key INTEGER NOT NULL
default_value TEXT NULL
max_length INTEGER NULL
precision_value INTEGER NULL
scale_value INTEGER NULL
FOREIGN KEY (table_id) REFERENCES database_tables(id) ON DELETE CASCADE
UNIQUE (table_id, column_name)
```

### Context Records

Context needs to become a first-class persistence feature, not a field hidden inside a connection object.

- [x] Use `context_records` as the source of truth for current database and table context. The legacy `database_connections.context` column remains only for schema compatibility and is no longer written as context storage.
- [x] Support database-level context.
- [x] Support table-level context.
- [x] Support manually authored and model-generated context records.
- [ ] Track provider used for generated context.
- [ ] Track prompt used for generation when useful, but do not store secrets.
- [x] Track timestamps and update source.
- [x] Add table context into chat prompt construction.
- [x] Add table context into MCP table discovery responses.
- [x] Context history is intentionally out of scope for v0.2.0 per product decision; current context records are sufficient.

Suggested tables:

```text
context_records
id TEXT PRIMARY KEY
database_id TEXT NOT NULL
table_id TEXT NULL
scope TEXT NOT NULL
context TEXT NOT NULL
source TEXT NOT NULL
provider_id TEXT NULL
prompt TEXT NULL
created_utc TEXT NOT NULL
updated_utc TEXT NOT NULL
FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE
FOREIGN KEY (table_id) REFERENCES database_tables(id) ON DELETE CASCADE
```

No `context_history` table is required for v0.2.0.

### Setup Wizard State

- [ ] Create `setup_state`.
- [ ] Store whether setup has completed, when it completed, and current step for resumable setup.
- [ ] Store the selected provider ID and database ID used during first-run setup.
- [ ] Store dismissal state only after the user intentionally completes or skips where allowed.
- [ ] Add a relaunch path from the dashboard header or Settings page.

Suggested columns:

```text
id TEXT PRIMARY KEY
status TEXT NOT NULL
current_step TEXT NULL
selected_provider_id TEXT NULL
selected_database_id TEXT NULL
completed_utc TEXT NULL
dismissed_utc TEXT NULL
updated_utc TEXT NOT NULL
```

## Persistence Driver and Methods

### Driver Lifecycle

- [ ] Add `DatabaseDriverBase` under `Tablix.Core.Persistence`.
- [ ] Add public protected-set properties:
  - [ ] `IModelProviderMethods ModelProviders`
  - [ ] `IDatabaseConnectionMethods DatabaseConnections`
  - [ ] `IDatabaseMetadataMethods DatabaseMetadata`
  - [ ] `IDatabaseContextMethods DatabaseContexts`
  - [ ] `ITableContextMethods TableContexts`
  - [ ] `ISetupStateMethods SetupState`
  - [ ] `ISettingsStateMethods SettingsState`
- [ ] Add `InitializeAsync(CancellationToken token = default)`.
- [ ] Add `CloseAsync(CancellationToken token = default)`.
- [ ] Add `ExecuteQueryAsync` and `ExecuteQueriesAsync` only if needed for migration/test utilities; do not expose generic CRUD as the primary abstraction.
- [ ] Add full dispose pattern.
- [ ] Add `SqliteDatabaseDriver` that opens connections per operation or holds a managed connection strategy with clear disposal rules.
- [ ] Configure SQLite with:
  - [ ] `PRAGMA foreign_keys = ON`
  - [ ] `PRAGMA journal_mode = DELETE`
  - [ ] `PRAGMA busy_timeout = 5000` or configurable timeout
  - [ ] No WAL mode
- [ ] Use `SemaphoreSlim` to serialize writes.
- [ ] Document thread-safety guarantees on the driver.

### Method Interfaces

- [ ] `IModelProviderMethods`
  - [ ] Create provider.
  - [ ] Read by ID.
  - [ ] Enumerate with pagination/filtering.
  - [ ] Update provider with write-only API key handling.
  - [ ] Delete provider.
  - [ ] Set enabled/default behavior if needed.
  - [ ] Test connectivity using PolyPrompt.
- [ ] `IDatabaseConnectionMethods`
  - [ ] Create database connection.
  - [ ] Read by ID.
  - [ ] Enumerate with pagination/filtering.
  - [ ] Update connection with write-only credential handling.
  - [ ] Delete connection and cascade metadata/context.
  - [ ] Test connectivity using the existing crawler/driver factory.
- [ ] `IDatabaseMetadataMethods`
  - [ ] Save crawl run.
  - [ ] Replace metadata from `DatabaseDetail`.
  - [ ] Read latest detail by database ID.
  - [ ] Enumerate tables with filters and pagination.
  - [ ] Read one table by table ID or schema/name.
  - [ ] Enumerate columns for table.
  - [ ] Enumerate relationships with filters and pagination.
- [ ] `IDatabaseContextMethods`
  - [ ] Read database context.
  - [ ] Upsert database context.
  - [ ] Append database context.
  - [x] Context history method is intentionally not required for v0.2.0.
- [ ] `ITableContextMethods`
  - [ ] Read table context.
  - [ ] Upsert table context.
  - [ ] Append table context.
  - [ ] Enumerate table contexts for a database.
  - [ ] Generate table context through chat provider workflow.
- [ ] `ISetupStateMethods`
  - [ ] Read setup state.
  - [ ] Update current step.
  - [ ] Mark completed.
  - [ ] Reset wizard state.
- [ ] `ISettingsStateMethods`
  - [ ] Store small mutable state that should not live in JSON, if needed later.

## Migration From JSON to SQLite

The first v0.2.0 build may run against a `tablix.json` created by earlier v0.2.0 pre-release builds. It must not silently lose configured providers or databases.

- [ ] On startup, initialize the SQLite persistence database before route/MCP registration.
- [ ] If the SQLite tables are empty and `tablix.json` contains legacy `Databases`, import them.
- [ ] If the SQLite tables are empty and `tablix.json` contains legacy `Chat.Providers`, import them.
- [ ] Import default sample database/provider when neither SQLite nor legacy JSON has records.
- [ ] After import, keep `tablix.json` readable but remove generated defaults from settings classes.
- [ ] Do not write plaintext credentials to logs during migration.
- [ ] Add migration tests for:
  - [ ] Fresh JSON plus empty DB seeds defaults.
  - [ ] Legacy JSON imports databases.
  - [ ] Legacy JSON imports providers.
  - [ ] Non-empty DB does not duplicate JSON imports.
  - [ ] Read APIs redact imported credentials.

## REST API Surface

Use typed request/response DTOs for every route. Paths should follow the existing Tablix convention: `/v1/...` with singular resource groups. Do not add route aliases.

### Health and Setup

- [ ] `GET /`
  - [ ] Returns server version, uptime, persistence database health, and setup completion.
- [ ] `GET /v1/setup`
  - [ ] Returns setup status, missing prerequisites, selected provider/database IDs, and recommended next step.
- [ ] `PUT /v1/setup`
  - [ ] Updates wizard step state.
- [ ] `POST /v1/setup/complete`
  - [ ] Marks wizard complete.
- [ ] `POST /v1/setup/reset`
  - [ ] Resets wizard state for testing/admin relaunch.

### Model Providers

- [ ] `GET /v1/model`
  - [ ] Paginated provider summaries.
  - [ ] Query: `maxResults`, `skip`, `filter`, `enabled`.
- [ ] `POST /v1/model`
  - [ ] Creates a provider.
  - [ ] Returns redacted read model.
- [ ] `GET /v1/model/{id}`
  - [ ] Reads redacted provider detail.
- [ ] `PUT /v1/model/{id}`
  - [ ] Updates provider.
  - [ ] Supports `ClearApiKey`.
  - [ ] Preserves existing API key when update omits it.
- [ ] `DELETE /v1/model/{id}`
  - [ ] Deletes provider unless it is actively referenced by default settings or running operation.
- [ ] `POST /v1/model/{id}/test`
  - [ ] Tests configured provider connectivity.
  - [ ] Uses PolyPrompt with a short validation prompt.
  - [ ] Returns latency, success/failure, provider type, model, and sanitized error.
- [ ] `POST /v1/model/test`
  - [ ] Tests provider settings before save for setup wizard/create modal.

### Configured Databases

- [ ] `GET /v1/database`
  - [ ] Paginated redacted database summaries.
  - [ ] Query: `maxResults`, `skip`, `filter`, `type`.
- [ ] `POST /v1/database`
  - [ ] Creates a configured database connection.
  - [ ] Does not trigger a background crawl unless explicitly requested by request flag.
- [ ] `GET /v1/database/{id}`
  - [ ] Reads redacted database detail plus latest crawl summary and database-level context.
- [ ] `PUT /v1/database/{id}`
  - [ ] Updates a configured database.
  - [ ] Preserves existing username/password when omitted.
  - [ ] Supports credential clearing fields.
- [ ] `DELETE /v1/database/{id}`
  - [ ] Deletes connection, crawl metadata, and context records.
- [ ] `POST /v1/database/{id}/test`
  - [ ] Tests configured database connectivity.
  - [ ] Returns success/failure, latency, sanitized error.
- [ ] `POST /v1/database/test`
  - [ ] Tests unsaved connection settings for setup wizard/create modal.

### Crawl and Metadata

- [ ] `POST /v1/database/{id}/crawl`
  - [ ] Non-streaming crawl.
  - [ ] Persists latest crawl metadata on completion.
- [ ] `POST /v1/database/{id}/crawl/stream`
  - [ ] SSE crawl progress.
  - [ ] Emits started, configuration loaded, table discovered, table examined, relationship inference, persistence, completed/failed events.
  - [ ] Persists metadata.
- [ ] `GET /v1/database/{id}/tables`
  - [ ] Paginated table metadata.
  - [ ] Includes table-level context summary and whether table context exists.
- [ ] `GET /v1/database/{id}/tables/{tableId}`
  - [ ] Table detail with columns, indexes, foreign keys, and context.
- [ ] `GET /v1/database/{id}/relationships`
  - [ ] Paginated relationship list.
- [ ] `GET /v1/database/{id}/metadata`
  - [ ] Full persisted database metadata, paginated or bounded to prevent huge responses.

### Context

- [x] `POST /v1/database/{id}/context`
  - [x] Replaces or appends database-level context in `context_records`.
- [x] `POST /v1/database/{id}/context/build`
  - [x] Generates database-level context using selected provider and last persisted crawl.
- [x] `GET /v1/database/{id}/table-context`
  - [x] Lists table-level context records.
- [x] `GET /v1/database/{id}/table-context/{tableId}`
  - [x] Reads one table-level context record.
- [x] `PUT /v1/database/{id}/table-context/{tableId}`
  - [x] Replaces or appends table-level context in `context_records`.
- [x] `POST /v1/database/{id}/table-context/{tableId}/build`
  - [x] Generates table context using selected provider and table metadata.
- [x] `POST /v1/database/{id}/table-context/build`
  - [x] Generates context for all tables or selected tables.
  - [ ] Consider SSE for long-running all-table context builds after v0.2.0.
- [x] No context history endpoint is required for v0.2.0.

### Chat

- [ ] Update `GET /v1/chat/options` to read providers/databases from SQLite.
- [ ] Update `POST /v1/chat` to read provider and database from SQLite.
- [ ] Update `POST /v1/chat/stream` to read provider and database from SQLite.
- [x] Include database context plus relevant table context in prompt preparation.
- [ ] Continue using persisted metadata for table/schema context before falling back to crawl.
- [ ] Do not add chat route aliases.

### Settings

- [ ] `GET /v1/settings`
  - [ ] Returns JSON-backed settings plus persistence health.
  - [ ] Does not include providers/databases.
- [ ] `PUT /v1/settings`
  - [ ] Updates JSON-backed server settings only.
  - [ ] Does not accept providers/databases.
  - [ ] Clearly reports restart-required settings.

## MCP Behavior

- [ ] Update MCP tool registrar dependencies from `SettingsManager` plus `CrawlCache` to a persistence-backed service or driver.
- [ ] `tablix_discover_databases` reads redacted database summaries from SQLite.
- [ ] `tablix_discover_database` reads persisted metadata and context from SQLite.
- [ ] `tablix_list_tables` reads persisted table metadata and table context summaries from SQLite.
- [ ] `tablix_discover_table` reads columns, indexes, foreign keys, and table context from SQLite.
- [ ] `tablix_list_relationships` reads persisted relationships from SQLite.
- [ ] `tablix_execute_query` reads connection settings from SQLite and continues enforcing allowed query operations.
- [ ] `tablix_update_context` supports database-level context and table-level context.
- [ ] Add or extend MCP request DTOs with optional `TableId`, `Schema`, and `TableName` for table context updates.
- [ ] Never expose provider API keys, target database usernames, or target database passwords in MCP responses.
- [ ] Update `MCP_API.md` with all changed tool behavior and examples.

## Dashboard Product Update

### Navigation

- [ ] Add topbar route `Models` between `Chat` and `Settings`.
- [ ] Keep topbar fixed at a stable height.
- [ ] Add route `/models`.
- [ ] Keep existing `Databases`, `Query`, `Chat`, and `Settings` routes working.
- [ ] Add setup wizard relaunch action from Settings or topbar utility area if it fits cleanly.

### Models Page

- [ ] Build `ModelsPage.tsx`.
- [ ] Use a table view with loading, empty, error, and populated states.
- [ ] Columns:
  - [ ] Name
  - [ ] Type
  - [ ] Endpoint
  - [ ] Model
  - [ ] Enabled
  - [ ] Native tools
  - [ ] Strict JSON
  - [ ] Last tested/result if stored
  - [ ] Actions menu
- [ ] Add `+ Add` action.
- [ ] Use modal for create, edit, and view.
- [ ] Include password-style API key field with reveal control.
- [ ] Include `Clear API key` option on edit.
- [ ] Include provider validation/test action in create/edit modal before save and after save.
- [ ] Show connectivity result with latency and sanitized error.
- [ ] Include row actions: View, Edit, Test, View JSON, Delete.
- [ ] Use a confirmation modal for delete.
- [ ] Avoid raw JSON editing for normal create/edit.

### Databases Page

- [ ] Update database list to call `/v1/database`.
- [ ] Use persisted summaries from SQLite.
- [ ] Keep row actions menu rendering above table/workspace.
- [ ] Add database connectivity test action.
- [ ] Update create/edit modal or page to use new typed database APIs.
- [ ] Preserve credential redaction and "leave blank to keep existing" behavior.
- [ ] Keep crawl progress SSE display.
- [ ] Persist crawl metadata to SQLite and refresh table list after completion.

### Database Detail Page

- [ ] Show database-level context from `/v1/database/{id}/context`.
- [ ] Keep fixed-size monospace context viewer with copy control.
- [ ] Add database context edit modal.
- [ ] Add database context build modal.
- [ ] Add table context column/indicator in tables list.
- [ ] Add table detail modal/drawer showing:
  - [ ] Columns
  - [ ] Indexes
  - [ ] Foreign keys
  - [ ] Table-level context
- [x] Add table context edit action.
- [x] Add table context build action.
- [ ] Add bulk "Build table context" action for all or selected tables.
- [ ] Show status and progress for table context generation.

### Settings Page

- [ ] Remove model provider editor from Settings.
- [ ] Remove database list/editor concerns from Settings if any remain.
- [ ] Show persistence database settings in a form section.
- [ ] Annotate persistence type/filename as restart-required.
- [ ] Keep REST, logging, API keys, chat global settings, and prompt-processing settings.
- [ ] Make clear that providers are managed on the Models page and databases on the Databases page.

### Setup Wizard

The setup wizard must do real work inside the workflow, not just point the user to pages.

- [ ] Detect first-run state after login by calling `/v1/setup`.
- [ ] Open a modal-driven wizard when setup is incomplete.
- [ ] Make the modal large enough for forms and progress output without page scrolling.
- [ ] Step 1: Model provider.
  - [ ] Create or select provider.
  - [ ] Form fields match Models modal.
  - [ ] Test provider connectivity through API.
  - [ ] Block Next until connectivity succeeds or user explicitly acknowledges a failed optional test if product policy allows.
- [ ] Step 2: Database connection.
  - [ ] Create or select database.
  - [ ] Form fields match database create/edit.
  - [ ] Test database connectivity through API.
  - [ ] Block Next until connectivity succeeds.
- [ ] Step 3: Crawl database.
  - [ ] Start SSE crawl.
  - [ ] Show fixed-height progress log and progress bar.
  - [ ] Persist crawl metadata.
  - [ ] Block Next until crawl completes successfully or user acknowledges degraded state.
- [ ] Step 4: Generate database context.
  - [ ] Show editable prompt.
  - [ ] Generate context using selected provider and latest crawl.
  - [ ] Display generated context.
  - [ ] Let user edit before saving.
  - [ ] Persist database-level context.
- [x] Step 5: Generate table contexts.
  - [ ] List tables from latest crawl.
  - [ ] Offer all tables selected by default with ability to unselect.
  - [x] Generate per-table context using selected provider.
  - [ ] Show per-table status, progress, errors, and saved state.
  - [ ] Persist table-level context.
- [ ] Step 6: Completion.
  - [ ] Mark setup complete.
  - [ ] Use polished copy such as: "Setup is complete. Open Chat when you are ready to ask questions about this database."
  - [ ] Provide primary action to Chat and secondary action to stay on dashboard.
- [ ] Persist wizard progress after each successful step.
- [ ] Provide a safe close confirmation if setup is in progress.
- [ ] Add tests for wizard first-run detection, route presence, and API calls.

### Chat Page

- [ ] Update provider dropdown to read from `/v1/model` or `/v1/chat/options`.
- [ ] Update database dropdown to read from `/v1/database` or `/v1/chat/options`.
- [ ] Reset conversation when selected provider/database changes.
- [ ] Display provider native-tool/fallback notices from persisted provider metadata.
- [x] Ensure chat prompt preparation includes database and table context.
- [ ] Keep markdown rendering, telemetry info icon, tool-call displays, streaming/non-streaming support, and adaptive scroll behavior.

## Docker and Factory Data

- [ ] Add `docker/tablix.db` seed file.
- [ ] Add `docker/factory/tablix.db` seed file.
- [ ] Keep existing `docker/database.db` as the sample target database.
- [ ] Keep existing `docker/factory/database.db` as the factory sample target database.
- [ ] Update `docker/compose.yaml` server volumes:
  - [ ] `./tablix.json:/app/tablix.json`
  - [ ] `./tablix.db:/app/tablix.db`
  - [ ] `./database.db:/app/database.db`
  - [ ] `./logs:/app/logs`
- [ ] Ensure the SQLite persistence filename in `docker/tablix.json` is `tablix.db`.
- [ ] Ensure server startup creates `tablix.db` if missing.
- [ ] Ensure factory seed database is valid and includes default sample provider/database records.
- [ ] Ensure seed database does not use WAL and ships as one file.
- [ ] Update `docker/factory/reset.bat`:
  - [ ] Stop containers.
  - [ ] Restore `database.db`.
  - [ ] Restore `tablix.db`.
  - [ ] Restore `tablix.json`.
  - [ ] Remove `tablix.db-wal`, `tablix.db-shm`, and any other SQLite sidecars if present.
  - [ ] Clear logs.
- [ ] Update `docker/factory/reset.sh` with the same behavior.
- [ ] Update `docker/update.bat` only if image/volume expectations change.
- [ ] Add a Docker validation test or documented manual check:
  - [ ] `docker compose -f docker/compose.yaml config --quiet`
  - [ ] reset scripts restore both JSON and SQLite files.

## Backend Integration Points

### Server Startup

- [ ] Load JSON settings.
- [ ] Resolve persistence DB filename.
- [ ] Initialize persistence driver.
- [ ] Run migrations.
- [ ] Run first-boot seed/migration from legacy JSON if applicable.
- [ ] Initialize logging.
- [ ] Initialize crawl service with persistence dependency.
- [ ] Register REST routes with persistence-backed handlers.
- [ ] Register MCP tools with persistence-backed access.
- [ ] Avoid eager crawling all databases on startup when persisted metadata exists unless settings explicitly request refresh.
- [ ] Health endpoint should report persistence initialization success/failure.

### Crawl Cache Replacement

`CrawlCache` should stop being the authoritative schema store. Either rename it to a crawl service or keep a short-lived in-memory acceleration layer backed by SQLite.

- [ ] Persist every successful crawl.
- [ ] Read existing metadata from SQLite for list/detail/chat/MCP operations.
- [ ] Use in-memory cache only as an optimization.
- [ ] Keep cache invalidation on database update/delete/crawl.
- [ ] Add a clear policy for degraded crawl:
  - [ ] Save failed crawl run with error.
  - [ ] Keep last successful table metadata.
  - [ ] Surface stale metadata status to dashboard and chat.

### Chat and Context Generation

- [ ] Provider lookup comes from persistence.
- [ ] Database lookup comes from persistence.
- [x] `ChatPreparation` reads persisted database context and table contexts.
- [ ] Build-context operations write to context tables.
- [x] Table context generation uses table-specific prompt and metadata.
- [x] Prompt text should distinguish database context from table context.
- [ ] Query execution continues using current target-database crawler drivers.

## Chat Prompt Processing and Tool Execution

Prompt processing is part of the v0.2.0 persistence redesign, not a separate chat-only feature. Provider capability settings, selected provider records, selected database records, persisted crawl metadata, database context, and table context all feed the model instructions and execution policy. The old standalone prompt-processing plan is consolidated here so one checklist governs the whole product.

### Objectives

- [ ] Use PolyPrompt `1.5.0` native tool chat when the selected persisted provider is configured for native tools.
- [ ] Keep server-side classification and planning as a fallback when native tool chat is unavailable, disabled, unsupported by the model, or omitted for an obvious data request.
- [ ] Stop relying on user-facing assistant prose to contain SQL before Tablix can execute a query.
- [ ] Decide explicitly whether the user asked for actual data, SQL text only, schema discussion, context discussion, or an allowed explicit write.
- [ ] Preserve explicit user intent when the user asks for SQL only or says not to run a query.
- [ ] Execute a permitted query when the user asks for data and Tablix has enough information to answer through query execution.
- [ ] Keep every execution governed by `AllowedQueries`, selected database scope, query validation, credential redaction, and telemetry.
- [ ] Make native/fallback execution visible in chat through inline tool-call timeline entries.

### PolyPrompt v1.5.0 Contract

- [ ] Keep `PolyPrompt` package reference at `1.5.0` or later.
- [ ] Use `CompletionClientBase.ToolChatAsync(ToolChatRequest request, CancellationToken token = default)`.
- [ ] Use `ToolDefinition.Function(...)` to declare Tablix tools.
- [ ] Use `ToolChatRequest.ToolChoice = "auto"` for native tool selection.
- [ ] Use `ToolChatRequest.ToolChoice = "none"` for final answer generation after tool results.
- [ ] Convert Tablix chat messages to PolyPrompt `ChatMessage` objects.
- [ ] Append `ToolChatResponse.ToAssistantMessage()` and `ChatMessage.ToolResult(...)` after executing model-requested tools.
- [ ] Treat PolyPrompt as protocol translation only; Tablix remains responsible for executing tools and enforcing policy.

### Target Chat Behavior

- [ ] If the user asks, "How many users are in the users table?", Tablix executes an allowed query and answers with the count.
- [ ] If native tool chat is enabled and the model emits `tablix_execute_query`, Tablix validates and executes that tool call.
- [ ] If native tool chat is enabled but the model returns text only for an obvious data request, Tablix falls back to server-side planning when `FallbackWhenNativeToolNotCalled` is enabled.
- [ ] If the user asks, "Show me the SQL to count users", Tablix returns SQL and does not execute.
- [ ] If the user says, "Do not run it, just give me the query", Tablix does not execute.
- [ ] If a planner or model tool call emits invalid SQL, Tablix reports a useful failure and does not send invalid SQL to the database.
- [ ] If execution fails because of unknown column, bad column, missing column, type mismatch, or ambiguous column, Tablix refreshes schema and retries once when safe.
- [ ] If refreshed schema proves persisted context is stale, Tablix updates context only when configured and safe or when the user requested a context update.
- [ ] If all chat execution paths are disabled, the assistant can discuss schema and draft SQL, but it cannot run queries.

### Prompt Processing Settings

- [ ] Keep global prompt-processing controls in JSON-backed `Chat.PromptProcessing`.
- [ ] Keep provider/model capability flags in persisted model provider records.
- [ ] Expose these prompt-processing fields in Settings:
  - [ ] `Enabled`
  - [ ] `PreferNativeToolCalls`
  - [ ] `RequireExecutionForDataRequests`
  - [ ] `AllowSqlOnlyByExplicitRequest`
  - [ ] `FallbackWhenNativeToolNotCalled`
  - [ ] `RetryAfterSchemaRefresh`
  - [ ] `MaxNativeToolIterations`
  - [ ] `MaxPlanningAttempts`
  - [ ] `PlannerTemperature`
- [ ] Clamp numeric settings:
  - [ ] `MaxNativeToolIterations`: 1-10
  - [ ] `MaxPlanningAttempts`: 1-5
  - [ ] `PlannerTemperature`: 0.0-1.0
- [ ] Default behavior:
  - [ ] `Enabled = true`
  - [ ] `PreferNativeToolCalls = true`
  - [ ] `RequireExecutionForDataRequests = true`
  - [ ] `AllowSqlOnlyByExplicitRequest = true`
  - [ ] `FallbackWhenNativeToolNotCalled = true`
  - [ ] `RetryAfterSchemaRefresh = true`
  - [ ] `MaxNativeToolIterations = 4`
  - [ ] `MaxPlanningAttempts = 2`
  - [ ] `PlannerTemperature = 0.0`

### Provider Capability Fields

- [ ] Persist provider capability fields in `model_providers`.
- [ ] Expose provider capability fields in Models API and Chat options:
  - [ ] `SupportsNativeToolCalls`
  - [ ] `UseNativeToolCalls`
  - [ ] `SupportsStrictJson`
  - [ ] `ToolCapabilityNote`
- [ ] Default local Ollama provider:
  - [ ] `SupportsNativeToolCalls = true`
  - [ ] `UseNativeToolCalls = false`
  - [ ] Note that support depends on selected local model.
- [ ] Default OpenAI provider:
  - [ ] `SupportsNativeToolCalls = true`
  - [ ] `UseNativeToolCalls = true`
  - [ ] `SupportsStrictJson = true`
- [ ] Default OpenAI-compatible provider:
  - [ ] `SupportsNativeToolCalls = true`
  - [ ] `UseNativeToolCalls = false`
  - [ ] Note that support depends on endpoint/model implementation.
- [ ] Default Gemini provider:
  - [ ] `SupportsNativeToolCalls = true`
  - [ ] `UseNativeToolCalls = true`
  - [ ] `SupportsStrictJson = true`
- [ ] Never block server-side fallback only because native tool calling is unsupported.

### Tool Definitions

- [ ] Keep a reusable `TablixChatToolDefinitions` builder.
- [ ] Define `tablix_execute_query` using `ToolDefinition.Function(...)`.
- [ ] Tool schema parameters:
  - [ ] `databaseId`: string, required
  - [ ] `query`: string, required
  - [ ] `purpose`: string, optional
- [ ] Tool description must tell the model:
  - [ ] Execute one permitted SQL statement against the selected database.
  - [ ] Use the tool when the user asks for actual data, counts, lists, totals, latest/top rows, summaries, or explicit allowed database changes.
  - [ ] Do not merely return SQL when the user asked for the answer.
  - [ ] Do not include semicolons.
  - [ ] Statement type must be in `AllowedQueries`.
  - [ ] Use only needed columns.
  - [ ] Aggregates do not need limits.
- [ ] Consider separate schema/context tools later only after the single query tool path is stable.

### Intent Classification

- [ ] Add `PromptIntentClassifier`.
- [ ] Add `PromptIntentTypeEnum`:
  - [ ] `Unknown`
  - [ ] `DataAnswerRequest`
  - [ ] `SqlOnlyRequest`
  - [ ] `SchemaQuestion`
  - [ ] `ContextQuestion`
  - [ ] `DatabaseConversation`
  - [ ] `ExplicitWriteRequest`
- [ ] Classify using deterministic user-text rules before choosing native/fallback/plain execution.
- [ ] Data phrases include `how many`, `count`, `show me`, `list`, `find`, `total`, `average`, `latest`, `top`, `which`, `who`, `what is`, `what are`, `summarize rows`, and `give me all`.
- [ ] SQL-only phrases include `show me the sql`, `write sql`, `generate sql`, `give me a query`, `what query`, `do not run`, `don't run`, and `sql only`.
- [ ] Schema phrases include `tables`, `columns`, `relationships`, `foreign keys`, `schema`, and `structure`.
- [ ] Write phrases include `insert`, `update`, `delete`, `add row`, `remove row`, and `change value`.
- [ ] Do not execute for `SqlOnlyRequest`.
- [ ] Require explicit user intent before executing allowed write statements.

### Native Tool Chat Orchestration

- [ ] Add `NativeToolChatOrchestrator` or split equivalent logic out of `ChatHandler`.
- [ ] Add tool definitions only when chat tools, prompt processing, native preference, provider support, and provider native use are enabled.
- [ ] Enforce `MaxNativeToolIterations`.
- [ ] Reject unknown tool names with a tool-result error message.
- [ ] Reject missing query arguments.
- [ ] Reject or force mismatched `databaseId`; model-provided arguments must never switch away from the selected database.
- [ ] Never expose provider API keys or target database credentials in tool arguments, tool results, prompts, logs, telemetry, or chat UI.
- [ ] Record each model-requested native call as a chat-visible tool-call timeline item.
- [ ] Fall back to server-side planning when native tool chat returns text only for a data request and fallback is enabled.

### Server-Side Fallback Planning

- [ ] Add `PromptQueryPlan` or align the existing fallback plan with this shape:
  - [ ] `ShouldExecute`
  - [ ] `StatementType`
  - [ ] `Sql`
  - [ ] `Reason`
  - [ ] `RequiredTables`
  - [ ] `RequiredColumns`
  - [ ] `NeedsClarification`
  - [ ] `ClarificationQuestion`
- [ ] Add `PromptProcessingResult` that captures:
  - [ ] `Intent`
  - [ ] `ExecutionPath`
  - [ ] `Plan`
  - [ ] `ToolCalls`
  - [ ] `Executed`
  - [ ] `Skipped`
  - [ ] `SkipReason`
- [ ] Build a hidden planner prompt requiring strict JSON only.
- [ ] Planner prompt must include selected database identity, type, schema, allowed query operations, persisted database context, relevant table contexts, table summaries, and relevant table geometry.
- [ ] Planner prompt must say: no markdown, no prose, JSON object only, one SQL statement, no semicolons, and do not plan execution for SQL-only requests.
- [ ] Parse planner output through the centralized serializer into named models.
- [ ] If parsing fails, retry once with a repair prompt.
- [ ] If repair fails, return a visible planning failure tool-call item.
- [ ] Normalize and validate SQL before execution.
- [ ] Reject execution when the statement type is not in `AllowedQueries`.
- [ ] Prefer explicit columns and limits for exploratory row-returning queries.
- [ ] Do not force limits on aggregate queries such as `COUNT`, `SUM`, or `AVG`.

### Shared Execution Enforcement

- [ ] Use one `ChatQueryExecutionService` for native tool calls, fallback planning, and MCP query execution where practical.
- [ ] Inputs include selected database connection, raw SQL, intent/write policy, and cancellation token.
- [ ] Responsibilities:
  - [ ] Normalize SQL.
  - [ ] Reject semicolons and multiple statements.
  - [ ] Validate statement type against `AllowedQueries`.
  - [ ] Enforce explicit write-request policy.
  - [ ] Execute through the correct target database crawler.
  - [ ] Truncate model-visible results according to chat tool settings.
  - [ ] Return a structured execution result.
- [ ] Execute write statements only when the user explicitly requested the write, the statement type is allowed, and SQL validation passes.
- [ ] Retry at most once after schema refresh for unknown column, bad column, missing column, type mismatch, or ambiguous column failures.
- [ ] Save failed execution details in tool telemetry without leaking credentials.

### Tool-Call Timeline and Streaming

- [ ] Standardize chat-visible tool-call names:
  - [ ] `tablix_classify_prompt`
  - [ ] `tablix_native_tool_chat`
  - [ ] `tablix_model_requested_tool`
  - [ ] `tablix_plan_query`
  - [ ] `tablix_validate_query`
  - [ ] `tablix_execute_query`
  - [ ] `tablix_refresh_schema`
  - [ ] `tablix_followup_answer`
- [ ] For each tool call, record ID, name, execution path, compact arguments, compact result, success/error, and elapsed milliseconds.
- [ ] Respect `Chat.Tools.MaxToolOutputCharacters`.
- [ ] Add or preserve SSE events:
  - [ ] `provider_notice`
  - [ ] `classification_started`
  - [ ] `classification_completed`
  - [ ] `native_tool_chat_started`
  - [ ] `native_tool_chat_completed`
  - [ ] `native_tool_requested`
  - [ ] `planning_started`
  - [ ] `planning_completed`
  - [ ] `tool_started`
  - [ ] `tool_completed`
  - [ ] `schema_refresh_started`
  - [ ] `schema_refresh_completed`
  - [ ] `token`
  - [ ] `completed`
  - [ ] `error`
- [ ] Existing dashboard clients should tolerate unknown event types.
- [ ] Waiting animation continues during native tool chat, fallback planning, schema refresh, and query execution.
- [ ] Final `completed` event includes telemetry, execution path, notices, and tool calls.
- [ ] Since PolyPrompt tool chat is non-streaming, stream Tablix progress events and return final text in completion unless a safe equivalent follow-up streaming call is implemented.

### Chat REST Contracts

- [ ] Keep existing endpoint paths:
  - [ ] `GET /v1/chat/options`
  - [ ] `POST /v1/chat`
  - [ ] `POST /v1/chat/stream`
- [ ] Do not add a public planning endpoint unless needed for diagnostics.
- [ ] If `POST /v1/chat/plan` is added later, gate it behind normal auth, mark it diagnostic, and document it separately.
- [ ] `ChatOptionsResponse` includes prompt-processing state and provider capability fields.
- [ ] `ChatResponseResult` includes prompt intent, execution decision/skip reason when useful, execution path, capability notice, telemetry, and tool calls.
- [ ] `ChatStreamEvent` includes prompt intent, notice/capability notice, execution decision/skip reason when useful, execution path, telemetry, and tool calls.

### MCP Alignment

- [ ] MCP tools keep their explicit protocol surface.
- [ ] MCP guidance says agents should execute permitted queries when the user asks for data.
- [ ] MCP guidance says agents should not merely return SQL unless the user requested SQL only.
- [ ] MCP query execution reuses the same validation and retry behavior where practical.
- [ ] MCP tools refresh schema and retry once for column/type errors when safe.

### Provider Connectivity Validation

- [ ] Add service method that builds a PolyPrompt client from unsaved or saved provider settings.
- [ ] Use a short prompt such as "Reply with the single word OK.".
- [ ] Enforce request timeout.
- [ ] Return success, duration, provider type, model, endpoint, and sanitized failure.
- [ ] Do not leak API key or Authorization header in error details.
- [ ] Expose through Models page and setup wizard.
- [ ] Add tests for missing endpoint/model, disabled provider, bad provider ID, and redaction.

### Database Connectivity Validation

- [ ] Add service method that builds an `IDatabaseCrawler` from unsaved or saved database settings.
- [ ] Use existing crawler `TestConnectionAsync`.
- [ ] Enforce timeout/cancellation.
- [ ] Return success, duration, database type, database name/filename, and sanitized failure.
- [ ] Expose through Databases page and setup wizard.
- [ ] Add tests for missing filename/hostname, bad provider type, and redaction.

## Postman Collection

- [ ] Reorganize `Tablix.postman_collection.json` folders:
  - [ ] Health
  - [ ] Setup
  - [ ] Models
  - [ ] Databases
  - [ ] Metadata
  - [ ] Context
  - [ ] Chat
  - [ ] Settings
  - [ ] MCP Information if applicable
- [ ] Add environment variables:
  - [ ] `baseUrl`
  - [ ] `apiKey`
  - [ ] `modelId`
  - [ ] `databaseId`
  - [ ] `tableId`
- [ ] Add create/update/read/delete provider examples.
- [ ] Add saved and unsaved provider test examples.
- [ ] Add create/update/read/delete database examples.
- [ ] Add saved and unsaved database test examples.
- [ ] Add crawl stream request note.
- [ ] Add database context read/update/build examples.
- [x] Add table context read/update/build examples.
- [ ] Update chat requests to use persisted provider/database IDs.
- [ ] Update Chat options example to show prompt-processing state and provider capabilities.
- [ ] Use a non-streaming chat example that should execute, such as `How many users are in the users table?`.
- [ ] Document native/fallback tool-call events in streaming chat examples.
- [ ] Ensure examples never include real credentials.

## Documentation Updates

### README.md

- [ ] Explain SQLite persistence and what remains in `tablix.json`.
- [ ] Document `Persistence.Type` and `Persistence.Filename`.
- [ ] Explain Docker-mounted `tablix.db`.
- [ ] Update dashboard description with Models page and setup wizard.
- [ ] Update first-run flow.
- [x] Update chat description to include database and table context.
- [ ] Document PolyPrompt native tool chat and server-side fallback behavior.
- [ ] Explain that PolyPrompt normalizes tool calls while Tablix executes and validates tools.
- [ ] Update credential redaction guarantees.

### GETTING_STARTED.md

- [ ] Rewrite the first-run flow around Docker, login, setup wizard, provider test, database test, crawl, database context, table context, and Chat.
- [ ] Explain how factory reset restores `tablix.json`, `tablix.db`, `database.db`, and logs.
- [x] Add troubleshooting for provider validation, database validation, crawl failures, and table context generation.
- [ ] Add troubleshooting for chat responses that only return SQL instead of executing an allowed query.
- [ ] Add a chat example where native tools or server fallback executes a query and shows tool-call progress.

### REST_API.md

- [ ] Document `/v1/...` as the only REST API convention.
- [ ] Do not document or implement route aliases.
- [ ] Add full Models API.
- [ ] Add full Databases API.
- [x] Add full Metadata API.
- [x] Add full Context API including table context.
- [ ] Add full Setup API.
- [ ] Update Settings API to remove providers/databases.
- [ ] Update Chat API to describe persisted context use.
- [ ] Update Chat options, response, and stream event models with prompt intent, execution path, capability notice, and tool-call behavior.
- [ ] Add redaction rules and examples.

### MCP_API.md

- [ ] Update discovery examples to reflect persisted metadata.
- [ ] Add table-level context behavior.
- [ ] Update context update tool arguments and responses.
- [ ] Re-state credential redaction.
- [ ] Clarify when models should crawl versus use persisted metadata.
- [ ] Align model guidance with execute-when-data-requested behavior.
- [ ] Explain schema refresh/retry guidance for column/type errors.

### CHANGELOG.md

- [ ] Add v0.2.0 entries for:
  - [ ] SQLite persistence layer.
  - [ ] Provider/database migration out of JSON.
  - [ ] Persisted crawl metadata.
  - [x] Database and table context.
  - [ ] Models page.
  - [ ] Setup wizard.
  - [ ] Docker `tablix.db` volume and factory reset.
  - [ ] PolyPrompt native tool chat, server fallback planning, and prompt-processing controls.
  - [ ] REST/Postman/MCP documentation expansion.
  - [ ] Test coverage additions.

## Testing Plan

All tests belong in `Test.Shared` and must be exposed through `Test.Automated`, `Test.Xunit`, and `Test.Nunit`.

### Persistence Unit and Integration Tests

- [ ] Driver initializes a missing SQLite file.
- [ ] Driver enforces `journal_mode=DELETE`.
- [ ] Driver does not create `-wal` or `-shm` files during normal operations.
- [ ] Migrations create all expected tables and indexes.
- [ ] Migrations are idempotent.
- [ ] Seed data is idempotent.
- [ ] Relative persistence filename resolves from settings file directory.
- [ ] Invalid persistence type fails with a clear error.
- [ ] SQLite write concurrency is serialized.

### Model Provider Tests

- [ ] Create provider.
- [ ] Read provider redacts API key.
- [ ] Enumerate providers paginates.
- [ ] Filter providers by name/type/enabled.
- [ ] Update provider preserves API key when omitted.
- [ ] Update provider clears API key when requested.
- [ ] Delete provider.
- [ ] Duplicate provider ID returns conflict.
- [ ] Provider validation endpoint returns sanitized failure.
- [ ] Provider validation endpoint never returns API key.

### Configured Database Tests

- [ ] Create database.
- [ ] Read database redacts username/password.
- [ ] Enumerate databases paginates.
- [ ] Filter databases.
- [ ] Update database preserves credentials when omitted.
- [ ] Update database clears credentials when requested.
- [ ] Delete database cascades metadata/context.
- [ ] Duplicate database ID returns conflict.
- [ ] Allowed query operations round-trip through child table.
- [ ] Connectivity validation succeeds against sample SQLite DB.
- [ ] Connectivity validation failure is sanitized.

### Crawl Metadata Tests

- [ ] Crawl persists database crawl run.
- [ ] Crawl persists tables.
- [ ] Crawl persists columns.
- [ ] Crawl persists indexes.
- [ ] Crawl persists foreign keys.
- [ ] Re-crawl replaces stale table metadata.
- [ ] Failed crawl records error while preserving last successful metadata.
- [ ] Table list reads from persistence without requiring in-memory cache.
- [ ] Relationship list reads from persistence.
- [ ] SSE crawl emits table-level progress events.

### Context Tests

- [ ] Database context create/read/update.
- [ ] Database context append mode.
- [ ] Table context create/read/update.
- [ ] Table context append mode.
- [x] Context history testing is intentionally not required for v0.2.0.
- [ ] Context delete/cascade behavior on database delete.
- [ ] Build database context persists generated text.
- [ ] Build table context persists generated text.
- [ ] Chat prompt includes database-level context.
- [ ] Chat prompt includes relevant table-level context.
- [ ] MCP table discovery includes table context.

### Prompt Processing Tests

- [ ] Classifier recognizes data request phrases.
- [ ] Classifier recognizes SQL-only phrases.
- [ ] Classifier recognizes schema question phrases.
- [ ] Classifier recognizes explicit write phrases.
- [ ] Classifier handles ambiguous phrases without unsafe execution.
- [ ] Native tool definition emits `tablix_execute_query`.
- [ ] Native tool schema contains required `databaseId` and `query`.
- [ ] Native tool description includes execute-not-describe guidance.
- [ ] Native tool argument handling executes valid arguments.
- [ ] Native tool argument handling rejects missing query.
- [ ] Native tool argument handling rejects or forces wrong database ID.
- [ ] Native tool argument handling rejects unknown tool names.
- [ ] Fallback planner parses valid JSON.
- [ ] Fallback planner parses fenced JSON.
- [ ] Fallback planner retries repair after invalid JSON.
- [ ] Fallback planner rejects missing SQL when execution is required.
- [ ] SQL validation rejects semicolons.
- [ ] SQL validation rejects disallowed statements.
- [ ] SQL validation executes allowed `SELECT`.
- [ ] SQL validation executes allowed `INSERT`, `UPDATE`, or `DELETE` only when explicitly requested.
- [ ] Provider capability projection includes native tool flags.
- [ ] Provider capability projection continues redacting API keys.
- [ ] Package guard verifies PolyPrompt `1.5.0` or later.

### Prompt Processing Integration Tests

- [ ] Native tool chat data request executes query and returns final answer.
- [ ] Native tool chat with no model tool call falls back to planner when required.
- [ ] Non-streaming fallback data request executes query and returns final answer.
- [ ] Streaming chat data request emits native/fallback/tool events before final answer.
- [ ] SQL-only request does not execute.
- [ ] Disabled chat tools produce a user-visible non-execution notice.
- [ ] Provider with `UseNativeToolCalls = false` produces informational notice while server-side fallback remains available.
- [ ] Unknown column error triggers one schema refresh/retry.
- [ ] Retry failure returns a useful error and does not loop.
- [ ] `How many users are in the users table?` executes.
- [ ] `Show me all database tables that begin with s` uses schema discovery, not row query execution unless needed.
- [ ] `Write SQL to count users` does not execute.
- [ ] `Do not run it; just give me the query` does not execute.
- [ ] `Delete inactive users` does not execute unless `DELETE` is allowed and user intent is explicit.
- [ ] Native tool path and fallback path produce equivalent final answers for a simple count query.

### REST Contract Tests

- [ ] Every dashboard API call matches a registered server route.
- [ ] `/v1/...` routes return expected status codes.
- [ ] No alternate REST route aliases are registered.
- [ ] Settings response excludes providers/databases.
- [x] OpenAPI includes Database, Metadata, Models, Context, Setup, Chat, Settings, and Health tags.
- [ ] Read routes never include credentials.
- [ ] Mutating routes use typed request bodies.
- [ ] Pagination fields are correct across all list routes.

### MCP Tests

- [ ] Database discovery reads persisted databases.
- [ ] Database discovery never returns credentials.
- [ ] Table list reads persisted table metadata.
- [ ] Table discovery includes table context.
- [ ] Relationship list reads persisted relationships.
- [ ] Query execution reads connection from persistence and enforces allowed operations.
- [ ] Context update supports database scope.
- [ ] Context update supports table scope.
- [ ] Bad tool arguments return typed error response.

### Dashboard Contract Tests

- [ ] Topbar includes Models between Chat and Settings.
- [ ] Models page lists providers from Models API.
- [ ] Models modal supports create/edit/view/test/delete actions.
- [ ] Settings page no longer includes provider editor.
- [ ] Settings page exposes prompt-processing fields.
- [ ] Setup wizard appears for incomplete setup.
- [ ] Setup wizard calls provider test API.
- [ ] Setup wizard calls database test API.
- [ ] Setup wizard listens to crawl SSE.
- [ ] Setup wizard saves database context.
- [x] Setup wizard saves table contexts.
- [x] Database detail page shows table context.
- [ ] Chat page uses persisted provider/database options.
- [ ] Chat page renders native tool capability notice.
- [ ] Chat page renders server-side fallback notice.
- [ ] Chat page renders native tool calls and fallback planning calls.
- [ ] Chat page warning appears when all execution paths are disabled.

### Docker and Factory Tests

- [ ] `docker/tablix.db` exists and opens with SQLite.
- [ ] `docker/factory/tablix.db` exists and opens with SQLite.
- [ ] `docker/compose.yaml` mounts `tablix.db`.
- [ ] `docker compose -f docker/compose.yaml config --quiet` passes.
- [ ] `reset.bat` restores `tablix.db`.
- [ ] `reset.sh` restores `tablix.db`.
- [ ] Reset scripts remove SQLite sidecar files.

### Structural Tests

- [x] No C# `var` declarations.
- [x] No tuple usage.
- [x] No partial class/interface/struct declarations.
- [x] No direct JSON DOM type usage.
- [x] No C# file contains more than one entity.
- [x] XML documentation builds with zero warnings.

## Implementation Sequence

### Phase 1: Persistence Foundation

- [x] Add persistence settings and enum.
- [x] Add persistence driver base, factory, SQLite driver, migrations, query classes, and method interfaces.
- [x] Add typed models for providers, databases, metadata, context, setup, and connectivity checks.
- [x] Add SQLite migrations.
- [x] Add seed/import service from legacy JSON.
- [x] Add persistence initialization to server startup.
- [x] Add low-level Touchstone tests.

### Phase 2: Backend API Migration

- [x] Route registration remains in the existing SwiftStack `InitializeRest()` style for v0.2.0; no route aliases were added.
- [x] Implement Models API.
- [x] Implement Databases API.
- [x] Implement Metadata API.
- [x] Implement Context API.
- [x] Implement Setup API.
- [x] Update Chat API to read provider/database/context from persistence.
- [x] Update Settings API to remove providers/databases.
- [x] Keep route registration on the existing `/v1/...` paths only.
- [x] Update OpenAPI metadata.
- [x] Add REST contract tests.

### Phase 3: MCP and Chat Integration

- [x] Update MCP registrar dependencies.
- [x] Update MCP tools to use persisted records.
- [x] Add table-level context support to MCP.
- [x] Implement prompt intent policy inside chat handling.
- [x] Implement native tool orchestration inside chat handling.
- [x] Implement fallback planner with JSON repair retry.
- [x] Reuse `ChatQueryExecutionService` across native and fallback chat execution.
- [x] Update chat preparation to include table context.
- [x] Update context generation to persist database/table contexts.
- [x] Add native/fallback tool-call timeline events.
- [x] Add prompt-processing streaming events.
- [x] Add MCP and chat tests.

### Phase 4: Dashboard

- [x] Update TypeScript types.
- [x] Update API client functions.
- [x] Add Models route/page/modal.
- [x] Update Settings page.
- [x] Update Databases list/detail/form flows.
- [x] Add table context management UI.
- [x] Add setup wizard.
- [x] Update Chat page options.
- [x] Add dashboard contract tests and run build.

### Phase 5: Docker and Seed Data

- [x] Generate `docker/tablix.db`.
- [x] Generate `docker/factory/tablix.db`.
- [x] Update `docker/compose.yaml`.
- [x] Update factory reset scripts.
- [x] Validate seed DB contents.
- [x] Validate compose config.

### Phase 6: Documentation and Postman

- [x] Update README.
- [x] Update GETTING_STARTED.
- [x] Update REST_API.
- [x] Update MCP_API.
- [x] Update CHANGELOG.
- [x] Update Postman collection.
- [x] Review all docs for consistency with actual API names and payloads.

### Phase 7: Final Validation

- [x] `dotnet build src\Tablix.slnx`
- [x] `dotnet run --no-build --project src\Test.Automated\Test.Automated.csproj`
- [x] `dotnet test src\Test.Xunit\Test.Xunit.csproj`
- [x] `dotnet test src\Test.Nunit\Test.Nunit.csproj`
- [x] `npm.cmd run build` in `dashboard`
- [x] JSON parse validation for `docker/tablix.json`, `docker/factory/tablix.json`, and `Tablix.postman_collection.json`
- [x] SQLite validation for `docker/tablix.db` and `docker/factory/tablix.db`
- [x] `docker compose -f docker\compose.yaml config --quiet`
- [x] C# structural scans for banned patterns and one-entity-per-file.
- [ ] Manual smoke test:
  - [ ] Start Docker compose.
  - [ ] Login to dashboard.
  - [ ] Complete setup wizard.
  - [ ] Confirm provider test passes.
  - [ ] Confirm database test passes.
  - [ ] Confirm crawl progress renders.
  - [ ] Confirm database context persists.
  - [ ] Confirm table context persists.
  - [ ] Ask a chat question that requires query execution.
  - [ ] Verify native tool-call timeline and final answer when the selected provider supports native tools.
  - [ ] Verify server fallback notice, execution timeline, and final answer when native tools are disabled or omitted.

## Release Readiness Criteria

- [x] `tablix.json` no longer contains model providers or configured databases in default/factory/server templates.
- [x] `tablix.db` is the source of truth for providers, databases, metadata, and context.
- [ ] Docker deployment works from a clean checkout with `docker compose pull && docker compose up`.
- [x] First-login setup wizard implements the provider, database, crawl, database context, table context, and chat-readiness path; manual browser smoke test remains open.
- [x] Dashboard Models page exists and manages providers independently of Settings.
- [x] Database detail page exposes table context management.
- [x] Chat executes allowed data requests through native PolyPrompt tools or server-side fallback.
- [x] SQL-only requests do not execute.
- [x] Chat tool-call timeline is visible in streaming and non-streaming responses.
- [x] Prompt-processing tests cover native path, fallback path, classifier, planning, execution, no-execution, provider notices, and settings.
- [x] REST, MCP, README, GETTING_STARTED, CHANGELOG, Postman, and dashboard behavior all describe the same API and storage model.
- [x] Automated, xUnit, NUnit, dashboard build, Docker config, JSON validation, SQLite validation, and structural scans all pass.
