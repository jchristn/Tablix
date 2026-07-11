<p align="center">
  <img src="https://raw.githubusercontent.com/jchristn/Tablix/main/assets/logo.png" alt="Tablix" height="192" />
</p>

<p align="center">
  <b>v0.2.0 - ALPHA</b> - API and structure may change without notice
</p>

<p align="center">
  <b>Authors:</b> <a href="https://github.com/jchristn">@jchristn</a> <a href="https://github.com/rawingate">@rawingate</a>
</p>

# Tablix

Tablix is a database discovery and query platform that connects your databases to AI agents and humans through REST and MCP interfaces.

## What's New in v0.2.0

v0.2.0 focuses on making Tablix reliable for larger databases and easier to validate in automation:

- **Large-schema-safe discovery:** agents can page through compact table and relationship indexes before requesting full table geometry.
- **Database context persistence:** agents can update the saved `Context` for a database after explicit user direction and schema analysis.
- **Relationship-aware workflows:** declared foreign keys are exposed as compact relationship edges for faster join planning.
- **Dashboard Chat and Settings:** users can chat with a selected database through configured PolyPrompt providers and edit server settings from structured forms.
- **Dashboard productivity controls:** crawl progress streams table-level status, database context can be built/copied from the UI, query results can be copied as JSON or downloaded as CSV, and the login page shows the configured server URL.
- **Touchstone test infrastructure:** shared tests now run through the CLI, xUnit, and NUnit from one source of truth.

## What Is Tablix?

Tablix sits between your databases and your tools. It crawls database schemas - discovering tables, columns, primary keys, foreign keys, and indexes - and exposes that metadata alongside query execution through a REST API and an MCP server. A built-in dashboard provides a browser-based UI for the same operations.

**Supported databases:** SQLite, PostgreSQL, MySQL, SQL Server.

## Why Use Tablix?

- **Give AI agents database access.** Connect Tablix via MCP to Claude Code, Cursor, Codex, or Gemini. Your agent can discover what databases are available, understand their schemas, and run queries to answer your questions - without you writing SQL.
- **Centralize database discovery.** Configure all your database connections in one place with user-supplied context that describes what each database contains and how its tables relate to one another. AI agents use this context to figure out what queries to run.
- **Control what's allowed.** Each database entry specifies which SQL statement types are permitted (`SELECT`, `INSERT`, `UPDATE`, `DELETE`, etc.). Tablix validates every query before execution.
- **Inspect schemas visually.** The dashboard shows crawled table geometry - columns, types, primary keys, foreign keys, and indexes - in a clean, browsable interface with light and dark modes.
- **Chat with database context.** The dashboard Chat page uses PolyPrompt `1.5.0` providers stored in `tablix.db` to answer natural-language questions using saved database/table context, crawled schema metadata, native tool calls when supported, and server-side fallback execution when a model does not call a tool for an obvious data request.

## How It Works

1. Configure one or more database connections in the setup wizard, dashboard, REST API, or `tablix.db`
2. Tablix starts REST/MCP immediately, then crawls each configured database in the background and caches schema geometry
3. AI agents connect via MCP to discover databases and execute queries
4. Humans use the dashboard or REST API for the same operations
5. Query validation enforces the `AllowedQueries` list per database

## API References

- [REST_API.md](REST_API.md) documents every REST endpoint, request body, response shape, error contract, and pagination field.
- [MCP_API.md](MCP_API.md) documents every MCP tool, input schema, response shape, agent workflow, and model-facing safety guidance.
- Swagger UI is available at `/swagger` when the server is running.

REST read endpoints and MCP discovery tools intentionally redact database credentials. `User` and `Password` are accepted only in configuration write requests; read/discovery responses expose `HasUser` and `HasPassword` booleans instead.

## Getting Started

For a full step-by-step walkthrough that covers Docker deployment, model provider setup, database setup, crawling, context building, and chat, see [GETTING_STARTED.md](GETTING_STARTED.md).

### Running from Docker

The quickest way to get Tablix running is with Docker Compose. The setup includes a Tablix server, a dashboard UI, and a sample SQLite database.

```bash
git clone https://github.com/jchristn/Tablix.git
cd Tablix/docker
docker compose up -d
```

Once running, the following services are available:

| Service | URL |
|---------|-----|
| REST API | http://localhost:9100 |
| Swagger UI | http://localhost:9100/swagger |
| Dashboard | http://localhost:9101 |
| MCP | http://localhost:9102/rpc |

Default API key: `tablixadmin`

The sample SQLite database includes `users`, `orders`, and `line_items` tables so you can explore schema discovery and query execution immediately.

#### Docker Compose Details

The `docker/compose.yaml` starts two containers:

- **tablix-server** (`jchristn77/tablix-server`) - the REST API and MCP server. Ports 9100 (REST) and 9102 (MCP) are exposed. The bootstrap `tablix.json`, product-state `tablix.db`, sample `database.db`, and `logs/` directory are bind-mounted from the `docker/` directory so data persists across restarts.
- **tablix-ui** (`jchristn77/tablix-ui`) - the dashboard, served via nginx on port 9101. It proxies API calls to the server using the `TABLIX_SERVER_URL` environment variable and shows that configured URL on the login page.

Both containers include healthchecks that run every 10 seconds with a 2 second timeout. The healthcheck scripts require two consecutive successful heartbeats before reporting healthy and terminate the container after two consecutive failed heartbeats so Docker's restart policy can restart it. The UI depends on a healthy backend and applies a 15 second startup delay through `TABLIX_UI_STARTUP_DELAY_SECONDS`.

#### Running Individual Containers

To run the server standalone with `docker run`:

```bash
docker run -d \
  -p 9100:9100 \
  -p 9102:9102 \
  -v $(pwd)/tablix.json:/app/tablix.json \
  -v $(pwd)/tablix.db:/app/tablix.db \
  -v $(pwd)/database.db:/app/database.db \
  -v $(pwd)/logs:/app/logs \
  jchristn77/tablix-server:v0.2.0
```

To run the dashboard standalone:

```bash
docker run -d \
  -p 9101:9101 \
  -e TABLIX_SERVER_URL=http://host.docker.internal:9100 \
  jchristn77/tablix-ui:v0.2.0
```

#### Factory Reset

To restore the Docker environment to its default state (resets `tablix.json`, `tablix.db`, `database.db`, and logs to their original contents):

```bash
cd docker/factory
./reset.sh      # Linux/Mac
reset.bat       # Windows
```

#### Building Images

To build and push Docker images from source:

```bash
build-server.bat v0.2.0
build-dashboard.bat v0.2.0
```

### Running from Source

```bash
cd src
dotnet build
dotnet run --project Tablix.Server
```

The server creates a default `tablix.json` on first run for bootstrap settings and initializes `tablix.db` with default model providers, a sample SQLite database connection, setup state, and persistence schema. Swagger UI is available at http://localhost:9100/swagger.

The dashboard includes Databases, Query, Chat, Models, and Settings pages plus a first-run setup wizard. The database detail view shows saved database context from database-scope `context_records` in `tablix.db`, supports inline context edits through `POST /v1/database/{id}/context`, displays table-level context editors, can generate table context through `POST /v1/database/{id}/table-context/{tableId}/build`, and uses `POST /v1/database/{id}/crawl/stream` to show schema crawl progress in real time with per-table status. The Models page manages model providers and connectivity tests. The Databases page exposes row actions from an overflow menu, including Build Context and Delete. Build Context lets a user edit model instructions, generate context from the last successful crawl through a configured provider, and persist the result to SQLite. The Query page can copy result JSON or download result rows as CSV. The Chat page selects a database and provider, supports streaming and non-streaming responses, renders markdown, can execute permitted queries through PolyPrompt native tool calls or server-side fallback planning, displays inline tool calls, shows the execution path, and exposes per-message telemetry. The Settings page edits form-based bootstrap/server settings, prompt-processing settings, and chat-tool settings, and annotates values that are saved immediately but require server restart to affect active listeners, logging, or persistence filename/type.

### Running Dashboard Locally

```bash
cd dashboard
npm install
npm run dev
```

For local development, set `VITE_TABLIX_SERVER_URL` or `TABLIX_SERVER_URL` before starting Vite to prepopulate the login page server URL. The login page also lets the user override the server URL; edited values are stored in browser local storage. In Docker, the dashboard uses `TABLIX_SERVER_URL` for the nginx proxy target and shows it on the login page without forcing browser clients to call the internal container hostname directly.

### Running Tests

```bash
dotnet build src/Tablix.slnx
dotnet run --project src/Test.Automated/Test.Automated.csproj
dotnet test src/Test.Xunit/Test.Xunit.csproj
dotnet test src/Test.Nunit/Test.Nunit.csproj
```

Tests are defined once in `Test.Shared` using Touchstone descriptors and exposed through the console runner, xUnit adapter, and NUnit adapter.
The xUnit and NUnit projects are intentionally adapter surfaces over the same shared tests, so coverage changes should start in `src/Test.Shared`.

## Installing MCP

Tablix can automatically install its MCP configuration into supported AI clients:

```bash
dotnet run --project src/Tablix.Server -- --install-mcp
```

This detects and patches configuration for:

| Client | Config File |
|--------|------------|
| Claude Code | `~/.claude.json` |
| Cursor | `~/.cursor/mcp.json` |
| Codex | `~/.codex/config.json` |
| Gemini | `~/.gemini/settings.json` |

After installing or updating MCP configuration, restart your AI agent or client to pick up the changes.

To configure manually, add to your client's MCP settings:

```json
{
  "mcpServers": {
    "tablix": {
      "type": "http",
      "url": "http://localhost:9102/rpc"
    }
  }
}
```

### MCP Tools

Tablix exposes eleven MCP tools. The recommended discovery flow for AI agents is:

See [MCP_API.md](MCP_API.md) for the complete MCP tool contract, response schemas, examples, and model guidance.

1. **`tablix_discover_databases`** - List configured databases
2. **`tablix_list_tables`** - Page through compact table summaries
3. **`tablix_list_relationships`** - Page through compact declared relationship edges
4. **`tablix_discover_table`** - Get full geometry for specific tables
5. **`tablix_execute_query`** - Execute a SQL query once the schema is understood
6. **`tablix_get_database_context`** - Read database context for one or more databases
7. **`tablix_get_table_context`** - Read table context for one or more tables
8. **`tablix_update_database_context`** - Persist analyzed database context back to `tablix.db`
9. **`tablix_update_table_context`** - Persist analyzed table context back to `tablix.db`
10. **`tablix_update_context`** - General context update tool with `scope = Database` or `scope = Table`
11. **`tablix_discover_database`** - Full database geometry for small databases or explicit full-schema requests

#### Choosing the Right Discovery Tool

| Need | Use | Why |
|------|-----|-----|
| Find configured databases | `tablix_discover_databases` | Returns IDs, redacted metadata, allowed query types, crawl state, and saved context |
| Understand a large database safely | `tablix_list_tables` then `tablix_list_relationships` | Keeps responses compact and pageable |
| Inspect tables before writing SQL | `tablix_discover_table` | Returns full column, key, foreign-key, and index geometry for one table |
| Read database context explicitly | `tablix_get_database_context` | Returns durable database-level guidance for one or more databases |
| Read table context explicitly | `tablix_get_table_context` | Returns durable table-level guidance for one or more tables |
| Retrieve a complete small schema | `tablix_discover_database` | Convenient when the schema is known to fit comfortably in model context |
| Save human-approved database analysis | `tablix_update_database_context` | Persists curated database-level context back to `tablix.db` |
| Save human-approved table analysis | `tablix_update_table_context` | Persists curated table-level context back to `tablix.db` |

For large databases, prefer this loop:

1. Call `tablix_discover_databases` and select the database by `Id`.
2. Call `tablix_list_tables` with a conservative `maxResults` such as `50`.
3. If `EndOfResults` is false, call `tablix_list_tables` again with `skip` set to `NextSkip`.
4. Call `tablix_list_relationships` the same way to collect declared foreign-key edges.
5. Call `tablix_get_table_context` for tables needed by the user's question.
6. Call `tablix_discover_table` only for tables needed by the user's question.
7. Execute read-only exploratory SQL only after checking `AllowedQueries` and validating table geometry.
8. Call `tablix_update_database_context` or `tablix_update_table_context` only when the user asks to persist the analysis or refreshed schema proves saved context is stale.

#### Agent Best Practices

- Start with `tablix_discover_databases`; use the returned `Context` as authoritative user guidance and check `AllowedQueries` before executing SQL.
- Treat discovery metadata as intentionally redacted. Tablix never returns database usernames or passwords through MCP discovery; `HasUser` and `HasPassword` are booleans only.
- For large schemas, avoid full-database geometry. Use `tablix_list_tables` and `tablix_list_relationships`, following `NextSkip` until `EndOfResults` is true.
- Treat `tablix_list_tables` as a compact index, not enough information for most SQL. Call `tablix_discover_table` for every table you plan to select from, join, filter on, insert into, update, or delete from.
- Use `tablix_get_database_context` for database-level guidance and `tablix_get_table_context` for table-level guidance. Context improves interpretation but does not replace schema validation.
- Treat `tablix_list_relationships` as declared foreign-key evidence. If no relationship is returned, that only means no declared FK was discovered; implicit relationships may still exist.
- When inferring relationships from column names or business context, clearly label them as inferred in answers and saved context.
- Prefer `SELECT` for exploration. Only run writes when the user explicitly asks and the database `AllowedQueries` permits the statement type.
- Use `tablix_update_database_context` for database-level context and `tablix_update_table_context` for table-level context. `tablix_update_context` remains available for generic scoped workflows. Do not store secrets, raw query results, or unsupported guesses as facts.

#### `tablix_discover_databases`

List all configured databases with redacted metadata and user-supplied context. Credential values are never returned; `HasUser` and `HasPassword` only indicate whether credentials are configured.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `maxResults` | integer | No | Maximum results to return (1-1000, default 100) |
| `skip` | integer | No | Number of records to skip (default 0) |
| `filter` | string | No | Filter by database ID or name |

#### `tablix_discover_database`

Get schema geometry for a database. This can produce large responses; use only for small databases or explicit full-schema requests. For large databases, prefer `tablix_list_tables`, `tablix_list_relationships`, and `tablix_discover_table`.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `maxTables` | integer | No | Optional maximum number of tables to return (1-1000) |
| `skip` | integer | No | Optional number of tables to skip |

#### `tablix_list_tables`

List tables in a database with schema names, column counts, foreign key counts, index counts, and pagination metadata. Use this as the compact table index for large schemas.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `maxResults` | integer | No | Maximum tables to return (1-1000, default 100) |
| `skip` | integer | No | Number of tables to skip (default 0) |
| `filter` | string | No | Filter by table or schema name |
| `schema` | string | No | Filter by schema name |

Returns `MaxResults`, `Skip`, `TotalRecords`, `RecordsRemaining`, `EndOfResults`, and `NextSkip`. If `EndOfResults` is false, call the tool again with `skip` set to `NextSkip`.

#### `tablix_list_relationships`

List compact relationship edges for a database. The current implementation returns declared foreign keys with `FromTable`, `FromColumn`, `ToTable`, `ToColumn`, `Source`, and `Confidence`. Absence of a relationship means no declared FK was discovered, not proof that tables are unrelated.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `maxResults` | integer | No | Maximum relationships to return (1-1000, default 100) |
| `skip` | integer | No | Number of relationships to skip (default 0) |
| `filter` | string | No | Filter by table, column, schema, or constraint name |
| `schema` | string | No | Filter by source or target schema |
| `includeInferred` | boolean | No | Reserved for inferred relationships; currently returns declared FKs only |

#### `tablix_discover_table`

Get full geometry for a single table: columns, data types, primary keys, foreign keys, and indexes.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `tableName` | string | Yes | Table name to retrieve geometry for |

#### `tablix_execute_query`

Execute a SQL query against a database. The query must be a single statement with no semicolons, and the statement type must be in the database's `AllowedQueries` list. Validate relevant tables and columns first with `tablix_discover_table`.

When the user asks for actual data or a requested database change using phrases like "show me", "how many", "count", "list", "find", "total", "average", "latest", "top", "add", "update", or "delete", agents should execute a permitted query and report the returned result or write outcome instead of only providing SQL text.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `query` | string | Yes | SQL query to execute |

Returns `Success`, `RowsReturned`, `TotalMs`, and a `Data` object containing `Columns` and `Rows`.

#### `tablix_get_database_context`

Read database-level context for one database, multiple databases, or a paged set of configured databases.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | No | Single database entry ID |
| `databaseIds` | string[] | No | Multiple database entry IDs |
| `maxResults` | integer | No | Maximum contexts to return when listing |
| `skip` | integer | No | Number of contexts to skip when listing |
| `filter` | string | No | Filter by database ID or name |

#### `tablix_get_table_context`

Read table-level context for one table, multiple tables, or a paged set of table contexts.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `tableId` | string | No | Single table metadata ID |
| `tableIds` | string[] | No | Multiple table metadata IDs |
| `tableName` | string | No | Single table name |
| `tableNames` | string[] | No | Multiple table names |
| `includeEmpty` | boolean | No | Include crawled tables even when no table context exists |
| `maxResults` | integer | No | Maximum contexts to return when listing |
| `skip` | integer | No | Number of contexts to skip when listing |

#### `tablix_update_database_context`

Update database-level context. The context helps AI agents understand what the database contains, how its tables relate, and what queries are useful. Preserve human-provided facts, distinguish declared relationships from inferred relationships, and avoid secrets or raw query results.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `context` | string | Yes | New context description |
| `mode` | string | No | `replace` or `append` (default `replace`) |

#### `tablix_update_table_context`

Update table-level context for one or more tables.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `databaseId` | string | Yes | Database entry ID |
| `tableId` | string | No | Single table metadata ID |
| `tableName` | string | No | Single table name when `tableId` is unknown |
| `context` | string | Yes for single update | New table context |
| `mode` | string | No | `replace` or `append` (default `replace`) |
| `updates` | object[] | No | Batch table context updates |

#### `tablix_update_context`

General context update tool retained for compatibility. Prefer `tablix_update_database_context` and `tablix_update_table_context`; use `scope` set to `Database` or `Table` when using the generic tool.

## Configuration

Tablix uses `tablix.json` only for bootstrap/server settings. Product state such as model providers, configured databases, crawled metadata, database context, and table context lives in `tablix.db`.

```json
{
  "Persistence": {
    "Type": "Sqlite",
    "Filename": "tablix.db"
  },
  "Rest": {
    "Hostname": "*",
    "Port": 9100,
    "Ssl": false,
    "McpPort": 9102
  },
  "Logging": {
    "Servers": [],
    "ConsoleLogging": true,
    "FileLogging": true,
    "LogDirectory": "./logs/",
    "LogFilename": "tablix.log",
    "MinimumSeverity": 0,
    "EnableColors": true
  },
  "Chat": {
    "Enabled": true,
    "DefaultProviderId": "provider_ollama_local",
    "DefaultStreaming": true,
    "SystemPrompt": "You are Tablix, a database assistant...",
    "MaxContextTables": 100,
    "Tools": {
      "Enabled": true,
      "AllowReadOnlyQueries": true,
      "AllowContextUpdates": true,
      "MaxToolIterations": 8,
      "MaxToolCalls": 20,
      "ToolTimeoutMs": 30000,
      "MaxToolOutputCharacters": 12000
    },
    "PromptProcessing": {
      "Enabled": true,
      "PreferNativeToolCalls": true,
      "RequireExecutionForDataRequests": true,
      "AllowSqlOnlyByExplicitRequest": true,
      "FallbackWhenNativeToolNotCalled": true,
      "RetryAfterSchemaRefresh": true,
      "MaxNativeToolIterations": 4,
      "MaxPlanningAttempts": 2,
      "PlannerTemperature": 0
    }
  },
  "ApiKeys": ["tablixadmin"]
}
```

Model providers are managed through the dashboard **Models** page or `/v1/model`. Database connections are managed through the dashboard **Databases** page or `/v1/database`.

### Chat Settings

The `Chat` section is the configuration surface for the dashboard chat experience and prompt-processing behavior. Provider records are stored in `tablix.db`; the seeded Docker database includes provider templates for Ollama, OpenAI, OpenAI-compatible endpoints, and Gemini. Only the local Ollama provider is enabled by default; cloud providers are disabled until an endpoint, model, and API key are supplied.

Tablix uses PolyPrompt `1.5.0` for provider-normalized tool chat. When `Chat.PromptProcessing.PreferNativeToolCalls` is enabled and the selected persisted provider has native tool calls enabled, Tablix sends a `tablix_execute_query` tool definition to the model. Tablix still owns query validation, execution, `AllowedQueries` enforcement, schema-refresh retry, telemetry, and secret redaction. If native tools are unavailable or the model does not call a tool for a clear data request, `Chat.PromptProcessing.FallbackWhenNativeToolNotCalled` lets Tablix use server-side planning to generate and execute a permitted query.

The default `Chat.SystemPrompt` instructs the model to restrict conversation to the selected database, its structure, its contents, and their relationships. It tells the model to use database context for database-wide guidance, table context for table-specific guidance, and schema discovery as the source of truth for table names, column names, keys, indexes, and data types. It also instructs the model to execute an allowed query with the available Tablix query tool when the user asks for data that can be answered from the database, rather than merely returning SQL for the user to run. If query execution reports a bad or unknown column, missing column, or column type mismatch, the prompt tells the model to refresh schema by crawling or re-discovering relevant tables, then update database or table context when refreshed schema proves saved context stale. Keep those boundaries in custom prompts unless you intentionally want different behavior.

`Chat.PromptProcessing` fields:

| Field | Description |
|-------|-------------|
| `Enabled` | Enables chat prompt processing and tool orchestration |
| `PreferNativeToolCalls` | Prefer PolyPrompt native tool calls when provider settings allow them |
| `RequireExecutionForDataRequests` | Treat data-answer questions as executable when permitted |
| `AllowSqlOnlyByExplicitRequest` | Do not execute when the user explicitly asks only for SQL |
| `FallbackWhenNativeToolNotCalled` | Use server-side planning when native tools are unavailable or omitted |
| `RetryAfterSchemaRefresh` | Recrawl and retry once for schema-related query errors |
| `MaxNativeToolIterations` | Maximum native tool loop iterations |
| `MaxPlanningAttempts` | Maximum fallback planner attempts |
| `PlannerTemperature` | Temperature used by the fallback planner |

Each provider includes an explicit `ApiKey` field stored in `tablix.db`. Providers that do not require authentication, such as a typical local Ollama instance, should leave it empty. Providers that do require authentication, such as OpenAI, Gemini, and many OpenAI-compatible services, should store their token through the Models page or Models REST API.

| Field | Description |
|-------|-------------|
| `Id` | Stable provider ID referenced by `Chat.DefaultProviderId` |
| `Name` | Display name for the dashboard and logs |
| `Type` | `Ollama`, `OpenAI`, `OpenAICompatible`, or `Gemini` |
| `Endpoint` | Base provider endpoint URL |
| `ApiKey` | Provider authentication material; empty when auth is not required |
| `Model` | Default model name |
| `Enabled` | Whether the provider is selectable |
| `DefaultStreaming` | Whether chat should stream by default |
| `Temperature`, `TopP`, `MaxTokens` | Optional generation controls |
| `RequestTimeoutMs` | Provider request timeout |
| `SupportsNativeToolCalls` | Whether the provider/model is expected to support tool calls |
| `UseNativeToolCalls` | Whether Tablix should attempt PolyPrompt native tool calls |
| `SupportsStrictJson` | Whether the provider/model is expected to follow strict JSON planner output |
| `ToolCapabilityNote` | Human-readable note shown in Settings and Chat |

Provider API keys are secret-bearing settings. Treat provider API keys the same way as database passwords: protect `tablix.db`, prefer environment-specific provisioning where possible, and never paste secrets into shared examples or issue reports.

### Example Database API Payloads

Database entries are not stored in `tablix.json`; these examples are request bodies for `POST /v1/database` or `PUT /v1/database/{id}`.

**SQLite**
```json
{
  "Id": "db_my_sqlite",
  "Name": "Local App Database",
  "Type": "Sqlite",
  "Filename": "./myapp.db",
  "AllowedQueries": ["SELECT"],
  "Context": "Local SQLite database for the myapp application."
}
```

**PostgreSQL**
```json
{
  "Id": "db_my_postgres",
  "Name": "Staging Orders",
  "Type": "Postgresql",
  "Hostname": "pg.example.com",
  "Port": 5432,
  "User": "readonly",
  "Password": "secret",
  "DatabaseName": "orders",
  "Schema": "public",
  "AllowedQueries": ["SELECT"],
  "Context": "Staging PostgreSQL database for the orders service."
}
```

**MySQL**
```json
{
  "Id": "db_my_mysql",
  "Name": "Production Users",
  "Type": "Mysql",
  "Hostname": "mysql.example.com",
  "Port": 3306,
  "User": "readonly",
  "Password": "secret",
  "DatabaseName": "users",
  "AllowedQueries": ["SELECT"],
  "Context": "Production MySQL database for user accounts."
}
```

**SQL Server**
```json
{
  "Id": "db_my_sqlserver",
  "Name": "Analytics Warehouse",
  "Type": "SqlServer",
  "Hostname": "sql.example.com",
  "Port": 1433,
  "User": "readonly",
  "Password": "secret",
  "DatabaseName": "analytics",
  "Schema": "dbo",
  "AllowedQueries": ["SELECT"],
  "Context": "SQL Server analytics warehouse for reporting."
}
```

### Database Entry Fields

| Field | Description |
|-------|-------------|
| `Id` | Unique identifier (e.g. `db_myapp`) |
| `Type` | `Sqlite`, `Postgresql`, `Mysql`, or `SqlServer` |
| `Hostname` | Database host (network databases) |
| `Port` | Database port (network databases) |
| `User` | Database username |
| `Password` | Database password |
| `DatabaseName` | Database name |
| `Schema` | Schema name (default `public`) |
| `Filename` | File path (SQLite) |
| `AllowedQueries` | Permitted SQL statement types |
| `Context` | Optional database-level context persisted as a database-scope `context_records` row |

### Logging Settings

| Field | Description |
|-------|-------------|
| `Servers` | Array of syslog server objects (optional, default empty) |
| `ConsoleLogging` | Enable console output (default `true`) |
| `FileLogging` | Enable file logging (default `true`) |
| `LogDirectory` | Directory for log files (default `./logs/`) |
| `LogFilename` | Log filename (default `tablix.log`) |
| `MinimumSeverity` | 0 = debug, 1 = info, 2 = warn, 3 = error, 4 = alert, 5 = critical, 6 = emergency (default `0`) |
| `EnableColors` | Colored console output (default `true`) |

Each syslog server entry has `Hostname` and `Port`:

```json
{
  "Logging": {
    "Servers": [
      { "Hostname": "127.0.0.1", "Port": 514 }
    ],
    "ConsoleLogging": true,
    "FileLogging": true,
    "LogDirectory": "./logs/",
    "LogFilename": "tablix.log",
    "MinimumSeverity": 0,
    "EnableColors": true
  }
}
```

When syslog servers are configured, log messages are forwarded to each server in addition to console and file output.

### REST API

All endpoints except health checks require `Authorization: Bearer <api-key>`. See [REST_API.md](REST_API.md) for full request/response details and [MCP_API.md](MCP_API.md) for the complete MCP tool contract. A [Postman collection](Tablix.postman_collection.json) is included in the repository. Swagger UI is available at `/swagger` when the server is running.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/` | No | Health check with version, uptime |
| `HEAD` | `/` | No | Lightweight health check (200 OK) |
| `GET` | `/v1/setup` | Yes | Read first-run setup state |
| `PUT` | `/v1/setup` | Yes | Update first-run setup state |
| `POST` | `/v1/setup/complete` | Yes | Mark setup complete |
| `GET` | `/v1/model` | Yes | List model providers |
| `GET` | `/v1/model/{id}` | Yes | Read a redacted model provider |
| `POST` | `/v1/model` | Yes | Create a model provider |
| `PUT` | `/v1/model/{id}` | Yes | Update a model provider |
| `DELETE` | `/v1/model/{id}` | Yes | Delete a model provider |
| `POST` | `/v1/model/test` | Yes | Test unsaved model provider settings |
| `POST` | `/v1/model/{id}/test` | Yes | Test a saved model provider |
| `GET` | `/v1/database` | Yes | List databases (paginated) |
| `GET` | `/v1/database/{id}` | Yes | Get database details and schema geometry |
| `GET` | `/v1/database/{id}/tables` | Yes | List database tables (paginated) |
| `GET` | `/v1/database/{id}/relationships` | Yes | List database relationships (paginated) |
| `POST` | `/v1/database` | Yes | Add a database entry |
| `PUT` | `/v1/database/{id}` | Yes | Update a database entry |
| `POST` | `/v1/database/test` | Yes | Test unsaved database settings |
| `POST` | `/v1/database/{id}/test` | Yes | Test a saved database |
| `POST` | `/v1/database/{id}/context` | Yes | Update database context |
| `POST` | `/v1/database/{id}/context/build` | Yes | Generate and persist database context |
| `GET` | `/v1/database/{id}/table-context` | Yes | List table context records |
| `GET` | `/v1/database/{id}/table-context/{tableId}` | Yes | Read table context |
| `PUT` | `/v1/database/{id}/table-context/{tableId}` | Yes | Update table context |
| `POST` | `/v1/database/{id}/table-context/build` | Yes | Generate and persist table context records |
| `POST` | `/v1/database/{id}/table-context/{tableId}/build` | Yes | Generate and persist one table context record |
| `DELETE` | `/v1/database/{id}` | Yes | Delete a database entry |
| `POST` | `/v1/database/{id}/crawl` | Yes | Re-crawl database schema |
| `POST` | `/v1/database/{id}/crawl/stream` | Yes | Re-crawl database schema with SSE progress |
| `POST` | `/v1/database/{id}/query` | Yes | Execute a SQL query |
| `GET` | `/v1/chat/options` | Yes | List chat databases and redacted providers |
| `POST` | `/v1/chat` | Yes | Send a non-streaming database chat request |
| `POST` | `/v1/chat/stream` | Yes | Send a streaming database chat request |
| `GET` | `/v1/settings` | Yes | Read redacted form-editable server settings |
| `PUT` | `/v1/settings` | Yes | Update running server settings |

### Query Validation

- Only statement types listed in `AllowedQueries` are permitted
- Multi-statement queries (containing `;`) are rejected
- Leading SQL comments are stripped before validation
- **This is a heuristic safeguard, not a security boundary**; always use database-level permissions for production safety
- Database passwords and provider API keys in `tablix.db` are stored in cleartext for v0.2.0; protect the file with OS-level permissions

### Degraded State

Initial crawls run in the background after REST and MCP listeners start. If a database crawl fails on startup (unreachable host, bad credentials, missing file):

- The server continues to start; crawl failures are non-fatal
- The affected database reports `IsCrawled: false` with a `CrawlError` message
- Re-crawl at any time via `POST /v1/database/{id}/crawl` or the dashboard
- Query execution may still work even when the crawl has not completed


## Issues and Discussions

- Report bugs and request features at https://github.com/jchristn/Tablix/issues
- Start or join discussions at https://github.com/jchristn/Tablix/discussions

## License

[MIT License](LICENSE.md) - Copyright (c) 2026 Joel Christner and Adam Wingate
