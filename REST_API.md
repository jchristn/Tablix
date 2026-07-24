# Tablix REST API

All responses are JSON (`Content-Type: application/json`). Authenticated endpoints require the `Authorization: Bearer <api-key>` header. API keys for Tablix REST authentication are configured in the `ApiKeys` array in `tablix.json`.

Model provider credentials are separate from REST API keys. Provider authentication material is stored in `tablix.db` and managed through `/v1/model`; read APIs redact secret values and expose `HasApiKey`.

Interactive documentation is available at `/swagger` when the server is running. The MCP tool contract is documented separately in [MCP_API.md](MCP_API.md).

## Setup

Setup endpoints drive the first-login wizard.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/v1/setup` | Read setup state and whether the wizard should be shown |
| `PUT` | `/v1/setup` | Save wizard progress, selected provider, and selected database |
| `POST` | `/v1/setup/complete` | Mark first-run setup complete |
| `POST` | `/v1/setup/dismiss` | Dismiss the first-run setup wizard without completing setup |

## Models

Model provider records are stored in `tablix.db`. Read responses never include plaintext API keys.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/v1/model?maxResults=100&skip=0&filter=&enabled=` | List providers |
| `GET` | `/v1/model/{id}` | Read one redacted provider |
| `GET` | `/v1/model/health` | List transient provider health snapshots |
| `GET` | `/v1/model/{id}/health` | Read one transient provider health snapshot |
| `POST` | `/v1/model` | Create a provider |
| `PUT` | `/v1/model/{id}` | Update a provider; leave `ApiKey` empty to preserve it or set `ClearApiKey` |
| `DELETE` | `/v1/model/{id}` | Delete a provider |
| `POST` | `/v1/model/test` | Test unsaved provider settings |
| `POST` | `/v1/model/{id}/test` | Test a saved provider |

## Databases

Database connection records, crawl metadata, table metadata, relationships, and context records are stored in `tablix.db`. Read responses redact credentials.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/v1/database?maxResults=100&skip=0&filter=` | List persisted database records |
| `GET` | `/v1/database/{id}` | Read one redacted database record with cached crawl geometry |
| `POST` | `/v1/database` | Create a database record |
| `PUT` | `/v1/database/{id}` | Update a database record |
| `DELETE` | `/v1/database/{id}` | Delete a database record, crawl metadata, and context records |
| `POST` | `/v1/database/test` | Test unsaved database settings |
| `POST` | `/v1/database/{id}/test` | Test a saved database connection |
| `GET` | `/v1/database/{id}/tables` | List crawled table summaries |
| `GET` | `/v1/database/{id}/relationships` | List crawled relationship summaries |
| `GET` | `/v1/database/{id}/intelligence` | Read domain intelligence, relationship candidates, ambiguity signals, context quality, and optional agent pack |
| `GET` | `/v1/database/{id}/agent-pack` | Read MCP-ready agent instructions and starter questions |
| `POST` | `/v1/database/{id}/crawl` | Re-crawl database schema |
| `POST` | `/v1/database/{id}/crawl/stream` | Re-crawl database schema with SSE progress |
| `POST` | `/v1/database/{id}/query` | Execute a permitted query |

## Context

Database and table context are stored in `tablix.db`. REST exposes database and table context management for the dashboard and automation. MCP exposes the same persisted context concepts through `tablix_get_database_context`, `tablix_get_table_context`, `tablix_update_database_context`, `tablix_update_table_context`, and the generic scoped `tablix_update_context`; see [MCP_API.md](MCP_API.md) for model-facing request shapes, batch behavior, and guidance.

REST reads database-level context through `GET /v1/database/{id}` and writes it through `POST /v1/database/{id}/context`. REST reads table-level context through `GET /v1/database/{id}/table-context` and `GET /v1/database/{id}/table-context/{tableId}`, and writes one table context through `PUT /v1/database/{id}/table-context/{tableId}`. For batch table context generation with a model provider, use `POST /v1/database/{id}/table-context/build`.

| Method | Path | Purpose |
| --- | --- | --- |
| `POST` | `/v1/database/{id}/context` | Replace or append database-level context |
| `POST` | `/v1/database/{id}/context/build` | Generate database-level context from persisted crawl metadata |
| `GET` | `/v1/database/{id}/table-context` | List table-level contexts |
| `GET` | `/v1/database/{id}/table-context/{tableId}` | Read one table context |
| `PUT` | `/v1/database/{id}/table-context/{tableId}` | Replace or append table-level context |
| `POST` | `/v1/database/{id}/table-context/build` | Generate table-level context for all or selected tables |
| `POST` | `/v1/database/{id}/table-context/{tableId}/build` | Generate table-level context for one table |

## Error Responses

All error responses follow this structure:

```json
{
  "Error": "NotFound",
  "Message": "The requested resource was not found.",
  "StatusCode": 404,
  "Description": "Database 'db_foo' not found."
}
```

| Error | Status Code | Message |
|-------|-------------|---------|
| `AuthenticationFailed` | 401 | Authentication failed. Please check your credentials. |
| `BadRequest` | 400 | The request was malformed or invalid. |
| `Forbidden` | 403 | This action is not permitted. |
| `NotFound` | 404 | The requested resource was not found. |
| `Conflict` | 409 | A conflict occurred with an existing resource. |
| `InternalError` | 500 | An internal server error occurred. |

---

## Shared Schemas

### Pagination Envelope

Paginated responses use `EnumerationResult<T>` fields:

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether the operation succeeded |
| `MaxResults` | integer | Page size after clamping to `1-1000` |
| `Skip` | integer | Number of records skipped |
| `TotalRecords` | integer | Total matching records before page slicing |
| `RecordsRemaining` | integer | Records remaining after this page |
| `EndOfResults` | boolean | `true` when no further page remains |
| `NextSkip` | integer or null | Use this as `skip` for the next page; null on the final page |
| `TotalMs` | number | Server-side elapsed time in milliseconds |
| `Objects` | array | Page objects |

### DatabaseEntry

`DatabaseEntry` is the write/configuration shape accepted by `POST /v1/database` and `PUT /v1/database/{id}`. It may include credentials.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string | Unique database entry ID |
| `Name` | string or null | Human-readable display name |
| `Type` | string | `Sqlite`, `Postgresql`, `Mysql`, or `SqlServer` |
| `Hostname` | string or null | Hostname for network databases |
| `Port` | integer or null | Network database port, clamped to `1-65535` |
| `User` | string or null | Database username for write/configuration requests |
| `Password` | string or null | Database password for write/configuration requests |
| `DatabaseName` | string or null | Database/catalog name |
| `Schema` | string or null | Schema to crawl, usually `public`, `dbo`, or `main` |
| `Filename` | string or null | SQLite file path |
| `AllowedQueries` | string[] | Allowed SQL statement types |
| `Context` | string or null | Human-authored or curated database context persisted as a database-scope `context_records` row |

### DatabaseSummary

`DatabaseSummary` is the redacted read/discovery shape returned by database list, create, and update responses. It never includes `User` or `Password`.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string | Database entry ID |
| `Name` | string or null | Human-readable display name |
| `Type` | string | Database engine type |
| `Hostname` | string or null | Hostname for network databases |
| `Port` | integer or null | Network database port |
| `HasUser` | boolean | Whether a username is configured |
| `HasPassword` | boolean | Whether a password is configured |
| `DatabaseName` | string or null | Database/catalog name |
| `Schema` | string or null | Schema name |
| `Filename` | string or null | SQLite file path |
| `AllowedQueries` | string[] | Allowed SQL statement types |
| `Context` | string or null | Saved database context from database-scope `context_records` |
| `IsCrawled` | boolean | Whether cached schema crawl succeeded |
| `CrawlError` | string or null | Last crawl error, if any |

### DatabaseConnectivityTestRequest

| Field | Type | Description |
|-------|------|-------------|
| `Database` | `DatabaseEntry` | Unsaved database settings to validate without persisting |

### DatabaseConnectivityTestResponse

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether the connection test succeeded |
| `DatabaseId` | string or null | Database ID when known |
| `Message` | string or null | Sanitized success or status message |
| `Error` | string or null | Sanitized failure message |
| `TotalMs` | number | Runtime in milliseconds |

### DatabaseReadDetail

`DatabaseReadDetail` extends full crawl geometry with redacted settings fields. It is returned by `GET /v1/database/{id}` and never includes `User` or `Password`.

| Field | Type | Description |
|-------|------|-------------|
| `DatabaseId` | string | Database entry ID |
| `Type` | string | Database engine type |
| `DatabaseName` | string or null | Database/catalog name |
| `Schema` | string or null | Schema name |
| `Context` | string or null | Saved database context from database-scope `context_records` |
| `Tables` | `TableDetail[]` | Full discovered table geometry |
| `CrawledUtc` | string or null | Last successful crawl time in UTC |
| `IsCrawled` | boolean | Whether the schema crawl succeeded |
| `CrawlError` | string or null | Last crawl error, if any |
| `Name` | string or null | Human-readable display name |
| `Hostname` | string or null | Hostname for network databases |
| `Port` | integer or null | Network database port |
| `HasUser` | boolean | Whether a username is configured |
| `HasPassword` | boolean | Whether a password is configured |
| `Filename` | string or null | SQLite file path |
| `AllowedQueries` | string[] | Allowed SQL statement types |

### Table Geometry

`TableDetail` contains `TableName`, `SchemaName`, `Columns`, `ForeignKeys`, and `Indexes`.

`ColumnDetail` fields are `ColumnName`, `DataType`, `IsNullable`, `IsPrimaryKey`, `DefaultValue`, and `MaxLength`.

`ForeignKeyDetail` fields are `ConstraintName`, `ColumnName`, `ReferencedTable`, and `ReferencedColumn`.

`IndexDetail` fields are `IndexName`, `Columns`, and `IsUnique`.

### CrawlProgressEvent

`CrawlProgressEvent` is sent as the `data` payload for streamed crawl server-sent events.

| Field | Type | Description |
|-------|------|-------------|
| `EventType` | string | `started`, `progress`, `completed`, or `failed` |
| `Stage` | string | Stable crawl stage identifier |
| `DatabaseId` | string | Database entry ID |
| `Message` | string | Human-readable status message |
| `Percent` | integer | Progress percentage from `0` to `100` |
| `Terminal` | boolean | Whether the event ends the stream |
| `TotalMs` | number | Server-side elapsed time |
| `TableCount` | integer or null | Discovered table count when known |
| `TableName` | string or null | Table currently examined for table-level progress |
| `TableIndex` | integer or null | One-based table index for table-level progress |
| `RelationshipCount` | integer or null | Discovered declared relationship count when known |
| `Error` | string or null | Crawl error for failed or degraded events |
| `Detail` | `DatabaseDetail` or null | Final crawl detail on terminal events |

### Compact Discovery Shapes

`TableSummary` fields are `TableId`, `SchemaName`, `TableName`, `Columns`, `ForeignKeys`, and `Indexes`.

`TableDetail` fields are `TableId`, `TableName`, `SchemaName`, `Context`, `Columns`, `ForeignKeys`, and `Indexes`. `Context` is the current table-scope `context_records` value when one exists.

`RelationshipDetail` fields are `FromSchema`, `FromTable`, `FromColumn`, `ToSchema`, `ToTable`, `ToColumn`, `ConstraintName`, `Source`, and `Confidence`. `Source` is `declared_fk` for database-declared foreign keys and `inferred_name_match` for name-based inferred candidates.

### Chat Schemas

`ChatRequest` fields:

| Field | Type | Description |
|-------|------|-------------|
| `DatabaseId` | string | Database ID used for schema and context |
| `ProviderId` | string | Persisted model provider ID from `/v1/model` |
| `Messages` | array | Conversation messages with `Role` and `Content` |
| `Streaming` | boolean or null | Client streaming preference; null uses settings defaults |
| `PreferNativeToolCalls` | boolean or null | Optional per-request override for native PolyPrompt tool preference |
| `FallbackWhenNativeToolNotCalled` | boolean or null | Optional per-request override for server fallback when native tools are unavailable or omitted |

`ChatPromptPreviewResponse` fields:

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether prompt preview was prepared |
| `DatabaseId` | string | Selected database ID |
| `ProviderId` | string | Selected provider ID |
| `Model` | string or null | Provider model |
| `SystemPrompt` | string or null | Effective system prompt sent to the provider |
| `ContextPrompt` | string or null | Database/schema/context prompt sent with the conversation |
| `SystemPromptCharacters` | integer | System prompt character count |
| `ContextPromptCharacters` | integer | Context prompt character count |
| `SystemPromptEstimatedTokens` | integer | Estimated system prompt tokens |
| `ContextPromptEstimatedTokens` | integer | Estimated context prompt tokens |
| `ConversationMessages` | integer | Conversation messages included in the context prompt |
| `Error` | string or null | Error when unsuccessful |

`ChatTelemetry` fields:

| Field | Type | Description |
|-------|------|-------------|
| `TimeToFirstTokenMs` | integer or null | Time to first token |
| `TotalStreamingTimeMs` | integer or null | Total provider generation/streaming time |
| `InputTokens` | integer or null | Input tokens, provider-reported or estimated |
| `OutputTokens` | integer or null | Output tokens, provider-reported or estimated |
| `TotalTokens` | integer or null | Total tokens |
| `EstimatedTokens` | boolean | Whether token counts were estimated by Tablix |

Provider API keys are never returned by read endpoints. Redacted provider objects expose `HasApiKey` only.

`BuildContextRequest` fields:

| Field | Type | Description |
|-------|------|-------------|
| `ProviderId` | string or null | Enabled provider ID; null uses `Chat.DefaultProviderId` |
| `Prompt` | string or null | User-editable instructions that influence generated context |

`BuildContextResponse` fields:

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether context was generated and persisted |
| `DatabaseId` | string | Database ID |
| `ProviderId` | string | Provider used |
| `Context` | string or null | Generated context saved to `tablix.db` |
| `Model` | string or null | Provider model |
| `Telemetry` | `ChatTelemetry` or null | Generation telemetry |
| `Error` | string or null | Error when unsuccessful |

`BuildTableContextRequest` fields:

| Field | Type | Description |
|-------|------|-------------|
| `ProviderId` | string or null | Enabled provider ID; null uses `Chat.DefaultProviderId` |
| `Prompt` | string or null | User-editable instructions that influence generated table context |
| `TableIds` | string[] | Optional persisted table metadata IDs. Empty means every crawled table |

`BuildTableContextResponse` fields:

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether table context was generated and persisted |
| `DatabaseId` | string | Database ID |
| `ProviderId` | string | Provider used |
| `Model` | string or null | Provider model |
| `Objects` | `TableContextRead[]` | Generated table context records saved to `context_records` |
| `Telemetry` | `ChatTelemetry` or null | Aggregate generation telemetry |
| `Error` | string or null | Error when unsuccessful |

---

## Health

### `GET /`

Health check. No authentication required.

**Response** `200 OK`

```json
{
  "Name": "Tablix",
  "Version": "0.3.0",
  "StartTimeUtc": "2026-03-20T14:30:00.000Z",
  "Uptime": "01:23:45.678"
}
```

### `HEAD /`

Lightweight health check. No authentication required. Returns `200 OK` with no body.

---

## Setup

### `GET /v1/setup`

Read first-run setup state.

**Response** `200 OK`

```json
{
  "Id": "default",
  "Status": "InProgress",
  "CurrentStep": "database-context",
  "SelectedProviderId": "provider_ollama_local",
  "SelectedDatabaseId": "db_sample_sqlite",
  "CompletedUtc": null,
  "DismissedUtc": null,
  "UpdatedUtc": "2026-07-11T12:00:00Z",
  "ShouldShowWizard": true
}
```

### `PUT /v1/setup`

Persist setup wizard progress.

**Request Body**

```json
{
  "Status": "InProgress",
  "CurrentStep": "table-context",
  "SelectedProviderId": "provider_ollama_local",
  "SelectedDatabaseId": "db_sample_sqlite"
}
```

**Response** `200 OK` - returns `SetupStateRead`.

### `POST /v1/setup/complete`

Mark setup complete.

**Response** `200 OK` - returns `SetupStateRead` with `Status: "Complete"` and `ShouldShowWizard: false`.

### `POST /v1/setup/dismiss`

Dismiss the first-run setup wizard without marking setup complete. This lets users leave the wizard and configure providers/databases later from the dashboard.

**Response** `200 OK` - returns `SetupStateRead` with `DismissedUtc` populated and `ShouldShowWizard: false`.

---

## Models

Model providers are stored in `tablix.db`. Read APIs never return plaintext `ApiKey`; they expose `HasApiKey` only. Tablix runs background health checks for configured providers and keeps status/history in memory; health status is exposed on provider read/list responses and through the dedicated health endpoints.

### `GET /v1/model`

List model providers.

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | integer | 100 | Maximum results to return (1-1000) |
| `skip` | integer | 0 | Number of records to skip |
| `filter` | string | - | Filter by ID, name, endpoint, model, or type |
| `enabled` | boolean | - | Optional enabled-state filter |

**Response** `200 OK` - returns `EnumerationResult<ModelProviderSummary>`.

### `GET /v1/model/{id}`

Read one redacted provider.

**Response** `200 OK`

```json
{
  "Id": "provider_ollama_local",
  "Name": "Local Ollama",
  "Type": "Ollama",
  "Endpoint": "http://ollama:11434",
  "ApiKey": null,
  "HasApiKey": false,
  "Model": "gemma3:4b",
  "SystemPrompt": null,
  "Enabled": true,
  "DefaultStreaming": true,
  "SupportsNativeToolCalls": true,
  "UseNativeToolCalls": true,
  "SupportsStrictJson": false,
  "ToolCapabilityNote": null,
  "Temperature": 0.2,
  "TopP": null,
  "MaxTokens": 4096,
  "RequestTimeoutMs": 120000,
  "MaxConcurrentRequests": 1,
  "HealthCheckEnabled": true,
  "HealthCheckUrl": "http://ollama:11434/api/tags",
  "HealthCheckMethod": "GET",
  "HealthCheckIntervalMs": 5000,
  "HealthCheckTimeoutMs": 2000,
  "HealthCheckExpectedStatusCode": 200,
  "HealthyThreshold": 2,
  "UnhealthyThreshold": 2,
  "HealthCheckUseAuth": false,
  "Health": {
    "EndpointId": "provider_ollama_local",
    "EndpointName": "Local Ollama",
    "HealthCheckEnabled": true,
    "IsHealthy": true,
    "FirstCheckUtc": "2026-07-23T16:00:00Z",
    "LastCheckUtc": "2026-07-23T16:00:05Z",
    "LastHealthyUtc": "2026-07-23T16:00:05Z",
    "LastUnhealthyUtc": null,
    "LastStateChangeUtc": "2026-07-23T16:00:00Z",
    "TotalUptimeMs": 5000,
    "TotalDowntimeMs": 0,
    "UptimePercentage": 100,
    "ConsecutiveSuccesses": 2,
    "ConsecutiveFailures": 0,
    "LastError": null,
    "History": [
      { "TimestampUtc": "2026-07-23T16:00:00Z", "Success": true },
      { "TimestampUtc": "2026-07-23T16:00:05Z", "Success": true }
    ]
  }
}
```

### `GET /v1/model/health`

List transient health snapshots for configured model providers.

**Response** `200 OK` - returns `List<EndpointHealthStatus>`.

### `GET /v1/model/{id}/health`

Read one provider health snapshot.

**Response** `200 OK` - returns `EndpointHealthStatus`.

`EndpointHealthStatus` includes `EndpointId`, `EndpointName`, `HealthCheckEnabled`, `IsHealthy`, first/last check timestamps, last healthy/unhealthy timestamps, `TotalUptimeMs`, `TotalDowntimeMs`, `UptimePercentage`, consecutive success/failure counts, `LastError`, and recent `History` records with `TimestampUtc` and `Success`.

### `POST /v1/model`

Create a provider.

**Request Body**

```json
{
  "Id": "provider_ollama_local",
  "Name": "Local Ollama",
  "Type": "Ollama",
  "Endpoint": "http://ollama:11434",
  "ApiKey": "",
  "ClearApiKey": false,
  "Model": "gemma3:4b",
  "SystemPrompt": null,
  "Enabled": true,
  "DefaultStreaming": true,
  "SupportsNativeToolCalls": true,
  "UseNativeToolCalls": true,
  "SupportsStrictJson": false,
  "ToolCapabilityNote": null,
  "Temperature": 0.2,
  "TopP": null,
  "MaxTokens": 4096,
  "RequestTimeoutMs": 120000,
  "MaxConcurrentRequests": 1,
  "HealthCheckEnabled": true,
  "HealthCheckUrl": "http://ollama:11434/api/tags",
  "HealthCheckMethod": "GET",
  "HealthCheckIntervalMs": 5000,
  "HealthCheckTimeoutMs": 2000,
  "HealthCheckExpectedStatusCode": 200,
  "HealthyThreshold": 2,
  "UnhealthyThreshold": 2,
  "HealthCheckUseAuth": false
}
```

**Response** `201 Created` - returns `ModelProviderSummary`.

### `PUT /v1/model/{id}`

Update a provider. Leave `ApiKey` empty/null to preserve the current key. Set `ClearApiKey` to `true` to remove the saved key. `SystemPrompt` is an optional provider-specific base prompt; when set, chat requests using that provider use it instead of the global `Chat.SystemPrompt`, with mandatory Tablix query-execution and no-fabrication rules appended. If `SupportsNativeToolCalls` is true, dashboard create/edit flows default `UseNativeToolCalls` to true so capable providers execute through PolyPrompt native tools unless explicitly disabled.

`RequestTimeoutMs` is applied per provider request. Batch operations such as table-context generation may issue multiple provider requests and are bounded by `MaxConcurrentRequests` so the model endpoint is not overwhelmed. `MaxConcurrentRequests` is clamped from `1` to `16`; use `1` for local/single-GPU endpoints unless the model server is known to handle parallel requests.

Health checks use `HealthCheckUrl`, `HealthCheckMethod` (`GET` or `HEAD`), `HealthCheckIntervalMs`, `HealthCheckTimeoutMs`, `HealthCheckExpectedStatusCode`, `HealthyThreshold`, `UnhealthyThreshold`, and `HealthCheckUseAuth`. When `HealthCheckUrl` is omitted, Tablix derives a default from the provider endpoint: Ollama uses `/api/tags`, OpenAI and OpenAI-compatible providers use `/v1/models`, and Gemini uses `/v1beta/models`. Health status is transient process state; provider health configuration is persisted with the provider record.

**Response** `200 OK` - returns `ModelProviderSummary`.

### `DELETE /v1/model/{id}`

Delete a provider.

**Response** `204 No Content`

### `POST /v1/model/test`

Test unsaved provider settings.

**Request Body**

```json
{
  "Provider": {
    "Id": "provider_ollama_local",
    "Type": "Ollama",
    "Endpoint": "http://ollama:11434",
    "Model": "gemma3:4b"
  }
}
```

**Response** `200 OK`

```json
{
  "Success": true,
  "ProviderId": "provider_ollama_local",
  "Model": "gemma3:4b",
  "Message": "Provider returned a response.",
  "Error": null,
  "TotalMs": 125.4
}
```

### `POST /v1/model/{id}/test`

Test a saved provider. Response shape matches `/v1/model/test`.

---

## Database CRUD

### `GET /v1/database`

List all configured databases with pagination and optional filtering.

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | integer | 100 | Maximum results to return (1-1000) |
| `skip` | integer | 0 | Number of records to skip |
| `filter` | string | - | Filter by database ID or name (case-insensitive) |

**Response** `200 OK`

```json
{
  "Success": true,
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 2,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "NextSkip": null,
  "TotalMs": 0.5,
  "Objects": [
    {
      "Id": "db_sample_sqlite",
      "Name": "Sample E-Commerce",
      "Type": "Sqlite",
      "Hostname": null,
      "Port": null,
      "HasUser": false,
      "HasPassword": false,
      "DatabaseName": "sample",
      "Schema": "main",
      "Filename": "./database.db",
      "AllowedQueries": ["SELECT", "INSERT", "UPDATE", "DELETE"],
      "Context": "Sample e-commerce database..."
    }
  ]
}
```

### `GET /v1/database/{id}`

Get database details including saved context, redacted connection settings, and cached schema geometry. `Context` is read from the database-scope row in `context_records`. Credential values are never returned by read endpoints; `HasUser` and `HasPassword` only indicate whether credentials are configured.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Response** `200 OK`

```json
{
  "DatabaseId": "db_sample_sqlite",
  "Type": "Sqlite",
  "DatabaseName": "sample",
  "Schema": "main",
  "Context": "Sample e-commerce database...",
  "Tables": [
    {
      "TableName": "users",
      "SchemaName": "main",
      "Columns": [
        {
          "ColumnName": "Id",
          "DataType": "INTEGER",
          "IsPrimaryKey": true,
          "IsNullable": false,
          "DefaultValue": null
        }
      ],
      "ForeignKeys": [],
      "Indexes": []
    }
  ],
  "CrawledUtc": "2026-03-20T14:30:00.000Z",
  "IsCrawled": true,
  "CrawlError": null,
  "Name": "Sample E-Commerce",
  "Hostname": null,
  "Port": null,
  "HasUser": false,
  "HasPassword": false,
  "Filename": "./database.db",
  "AllowedQueries": ["SELECT", "INSERT", "UPDATE", "DELETE"]
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

### `GET /v1/database/{id}/tables`

List crawled tables for a database with pagination and optional filtering. This is the preferred REST endpoint for large schemas when callers do not need full table geometry for every table.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | integer | 100 | Maximum tables to return (1-1000) |
| `skip` | integer | 0 | Number of tables to skip |
| `filter` | string | - | Filter by table or schema name |
| `schema` | string | - | Filter by schema name |

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "Context": "Sample e-commerce database...",
  "IsCrawled": true,
  "TableCount": 3,
  "Filter": null,
  "Schema": null,
  "MaxResults": 2,
  "Skip": 0,
  "TotalRecords": 3,
  "RecordsRemaining": 1,
  "EndOfResults": false,
  "NextSkip": 2,
  "TotalMs": 0.3,
  "Objects": [
    {
      "TableId": "tbl_db_sample_sqlite_main_orders",
      "SchemaName": "main",
      "TableName": "orders",
      "Columns": 5,
      "ForeignKeys": 1,
      "Indexes": 0
    }
  ]
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

### `GET /v1/database/{id}/relationships`

List compact relationship edges for a database with pagination and optional filtering. Declared foreign keys are always returned. Set `includeInferred=true` to include name-based inferred relationship candidates with confidence scores.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxResults` | integer | 100 | Maximum relationships to return (1-1000) |
| `skip` | integer | 0 | Number of relationships to skip |
| `filter` | string | - | Filter by table, column, schema, or constraint name |
| `schema` | string | - | Filter by source or target schema |
| `includeInferred` | boolean | false | Include name-based inferred relationship candidates |

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "Context": "Sample e-commerce database...",
  "IsCrawled": true,
  "TableCount": 3,
  "Filter": null,
  "Schema": null,
  "IncludeInferred": true,
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 3,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "NextSkip": null,
  "TotalMs": 0.2,
  "Objects": [
    {
      "FromSchema": "main",
      "FromTable": "orders",
      "FromColumn": "UserId",
      "ToSchema": "main",
      "ToTable": "users",
      "ToColumn": "Id",
      "ConstraintName": "fk_orders_users",
      "Source": "declared_fk",
      "Confidence": 1.0
    },
    {
      "FromSchema": "main",
      "FromTable": "orders",
      "FromColumn": "customer_id",
      "ToSchema": "main",
      "ToTable": "customers",
      "ToColumn": "id",
      "Source": "inferred_name_match",
      "Confidence": 0.90
    }
  ]
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

### `GET /v1/database/{id}/intelligence`

Derive schema-to-domain intelligence from the last crawl and saved context. This response is deterministic and does not require a model provider.

**Query Parameters**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `includeAgentPack` | boolean | true | Include `AgentPack.Markdown`, instructions, and starter questions |

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "Domain": {
    "Summary": "Crawled 3 table(s), 2 declared relationship(s), and 1 inferred relationship candidate(s). Saved database context is available.",
    "Entities": [
      {
        "TableId": "tbl_db_sample_sqlite_main_orders",
        "SchemaName": "main",
        "TableName": "orders",
        "Role": "entity",
        "Summary": "Contains 5 column(s), 1 declared FK(s), saved context.",
        "KeyColumns": ["Id", "UserId", "CreatedAt", "TotalAmount"],
        "HasContext": true
      }
    ],
    "Workflows": ["main.orders joins to main.users through UserId -> Id"],
    "Metrics": ["main.orders.TotalAmount"],
    "CommonFilters": ["main.orders.CreatedAt"],
    "FreshnessColumns": ["main.orders.CreatedAt"],
    "TenantColumns": [],
    "SoftDeleteColumns": []
  },
  "Relationships": [
    {
      "FromSchema": "main",
      "FromTable": "orders",
      "FromColumn": "UserId",
      "ToSchema": "main",
      "ToTable": "users",
      "ToColumn": "Id",
      "Source": "declared_fk",
      "Confidence": 1.0
    }
  ],
  "Ambiguities": [
    {
      "Term": "latest",
      "Reason": "Multiple timestamp columns could define latest records.",
      "Question": "Which timestamp defines latest?",
      "Candidates": ["main.orders.CreatedAt", "main.orders.UpdatedAt"]
    }
  ],
  "ContextQuality": {
    "Score": 78,
    "Label": "Good",
    "TablesWithContext": 2,
    "TotalTables": 3,
    "DeclaredRelationships": 2,
    "InferredRelationships": 1,
    "Signals": []
  },
  "AgentPack": {
    "Success": true,
    "DatabaseId": "db_sample_sqlite",
    "Markdown": "# Tablix Agent Pack: Sample E-Commerce\n...",
    "Instructions": ["Start with tablix_discover_databases and select databaseId db_sample_sqlite."],
    "SuggestedQuestions": ["How many records are in main.orders?"]
  },
  "TotalMs": 1.2
}
```

### `GET /v1/database/{id}/agent-pack`

Return only the MCP-ready agent pack for one database. The pack includes selected-database instructions, safe discovery guidance, major entities, declared and inferred relationship notes, ambiguity warnings, and starter questions.

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "GeneratedUtc": "2026-07-22T14:12:00Z",
  "Markdown": "# Tablix Agent Pack: Sample E-Commerce\n...",
  "Instructions": [
    "Start with tablix_discover_databases and select databaseId db_sample_sqlite."
  ],
  "SuggestedQuestions": [
    "How many records are in main.orders?"
  ]
}
```

### `POST /v1/database`

Add a new database entry. An initial schema crawl is triggered automatically.

**Request Body**

```json
{
  "Id": "db_my_postgres",
  "Name": "My Postgres DB",
  "Type": "Postgresql",
  "Hostname": "pg.example.com",
  "Port": 5432,
  "User": "readonly",
  "Password": "secret",
  "DatabaseName": "mydb",
  "Schema": "public",
  "AllowedQueries": ["SELECT"],
  "Context": "Description for AI agents..."
}
```

**Response** `201 Created` - returns the created database summary with credentials redacted.

```json
{
  "Id": "db_my_postgres",
  "Name": "My Postgres DB",
  "Type": "Postgresql",
  "Hostname": "pg.example.com",
  "Port": 5432,
  "HasUser": true,
  "HasPassword": true,
  "DatabaseName": "mydb",
  "Schema": "public",
  "AllowedQueries": ["SELECT"],
  "Context": "Description for AI agents...",
  "IsCrawled": false,
  "CrawlError": null
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing |
| 409 | A database with the same ID already exists |

### `PUT /v1/database/{id}`

Update an existing database entry. The crawl cache is updated to reflect changes immediately. When updating an existing database, an empty or omitted `User` or `Password` preserves the stored value.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Request Body** - same structure as `POST /v1/database`. The `Id` field in the body is ignored; the path parameter is used.

**Response** `200 OK` - returns the updated database summary with credentials redacted.

```json
{
  "Id": "db_my_postgres",
  "Name": "Updated Name",
  "Type": "Postgresql",
  "Hostname": "pg.example.com",
  "Port": 5432,
  "HasUser": true,
  "HasPassword": true,
  "DatabaseName": "mydb",
  "Schema": "public",
  "AllowedQueries": ["SELECT", "INSERT", "UPDATE", "DELETE"],
  "Context": "Updated context description...",
  "IsCrawled": true,
  "CrawlError": null
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing |
| 404 | Database ID not found |

### `POST /v1/database/test`

Test unsaved database settings without persisting them. The request body wraps the same `DatabaseEntry` shape used by `POST /v1/database`.

**Request Body**

```json
{
  "Database": {
    "Id": "db_candidate",
    "Name": "Candidate SQLite",
    "Type": "Sqlite",
    "Filename": "./database.db",
    "Schema": "main",
    "AllowedQueries": ["SELECT"]
  }
}
```

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_candidate",
  "Message": "Connection test succeeded.",
  "Error": null,
  "TotalMs": 12.4
}
```

### `POST /v1/database/{id}/test`

Test a saved database connection. Response shape matches `/v1/database/test`.

### `POST /v1/database/{id}/context`

Update only the user-supplied database-level context. This is intended for AI-assisted schema analysis workflows where a model, optionally guided by human input, persists a concise database summary back to `tablix.db`.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Request Body**

```json
{
  "Context": "Analyzed database context...",
  "Mode": "replace"
}
```

`Mode` may be `replace` or `append`. The default is `replace`.

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "Context": "Analyzed database context...",
  "Mode": "replace"
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing or mode is unsupported |
| 404 | Database ID not found |

### `POST /v1/database/{id}/context/build`

Generate database context with a configured chat provider and persist it as a database-scope row in `context_records`. The prompt uses the last successful schema crawl; the endpoint returns `409` if the database has not been crawled successfully.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Request Body**

```json
{
  "ProviderId": "provider_ollama_local",
  "Prompt": "Analyze the crawled schema and produce concise durable context for future database chat."
}
```

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "ProviderId": "provider_ollama_local",
  "Context": "Sample e-commerce database with users, orders, and line_items...",
  "Model": "gemma3:4b",
  "Telemetry": {
    "TimeToFirstTokenMs": 550,
    "TotalStreamingTimeMs": 2200,
    "InputTokens": 900,
    "OutputTokens": 180,
    "TotalTokens": 1080,
    "EstimatedTokens": true
  },
  "Error": null
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing |
| 403 | Chat is disabled |
| 404 | Database ID or provider ID not found |
| 409 | No successful crawl is available |
| 502 | Provider context generation failed |

### `GET /v1/database/{id}/table-context`

List table-level context records for a database.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Response** `200 OK`

```json
[
  {
    "Id": "ctx_012345",
    "DatabaseId": "db_sample_sqlite",
    "TableId": "tbl_db_sample_sqlite_main_users",
    "SchemaName": "main",
    "TableName": "users",
    "Context": "Stores application users...",
    "Source": "user",
    "UpdatedUtc": "2026-07-11T12:00:00Z"
  }
]
```

### `GET /v1/database/{id}/table-context/{tableId}`

Read table-level context for one table.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |
| `tableId` | string | Persisted table metadata ID |

**Response** `200 OK` - returns `TableContextRead`.

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database or table context not found |

### `PUT /v1/database/{id}/table-context/{tableId}`

Replace or append table-level context.

**Request Body**

```json
{
  "Context": "Table-specific context, important columns, joins, filters, and caveats.",
  "Mode": "replace",
  "Source": "user"
}
```

`Mode` may be `replace` or `append`. `Source` is an optional label such as `user` or `model`.

**Response** `200 OK` - returns `TableContextRead`.

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing or mode is unsupported |
| 404 | Database or table not found |

### `POST /v1/database/{id}/table-context/build`

Generate table-level context with a configured chat provider and persist each generated record to `context_records` with `scope = "Table"`. If `TableIds` is empty or omitted, Tablix generates context for every crawled table that has persisted table metadata.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Request Body**

```json
{
  "ProviderId": "provider_ollama_local",
  "Prompt": "For each table, produce concise durable table context with key columns and join guidance.",
  "TableIds": ["tbl_db_sample_sqlite_main_users"]
}
```

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "ProviderId": "provider_ollama_local",
  "Model": "gemma3:4b",
  "Objects": [
    {
      "Id": "ctx_012345",
      "DatabaseId": "db_sample_sqlite",
      "TableId": "tbl_db_sample_sqlite_main_users",
      "SchemaName": "main",
      "TableName": "users",
      "Context": "Stores application users...",
      "Source": "model",
      "UpdatedUtc": "2026-07-11T12:00:00Z"
    }
  ],
  "Telemetry": {
    "TimeToFirstTokenMs": 1200,
    "TotalStreamingTimeMs": 3100,
    "InputTokens": 1200,
    "OutputTokens": 240,
    "TotalTokens": 1440,
    "EstimatedTokens": true
  },
  "Error": null
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing |
| 403 | Chat is disabled |
| 404 | Database ID or provider ID not found |
| 409 | No successful crawl or persisted table metadata is available |
| 502 | Provider table-context generation failed |

### `POST /v1/database/{id}/table-context/{tableId}/build`

Generate and persist context for exactly one crawled table. The request and response bodies match `/v1/database/{id}/table-context/build`; `TableIds` is ignored because the path selects the table.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |
| `tableId` | string | Persisted table metadata ID |

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing |
| 403 | Chat is disabled |
| 404 | Database ID, provider ID, or table ID not found |
| 409 | No successful crawl is available |
| 502 | Provider table-context generation failed |

### `DELETE /v1/database/{id}`

Delete a database entry and remove it from the crawl cache.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Response** `204 No Content`

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

---

## Schema Discovery

For large schemas, prefer the compact discovery flow:

1. Use `GET /v1/database/{id}/tables` to page through table summaries.
2. Use `GET /v1/database/{id}/relationships` to page through declared foreign-key edges, and set `includeInferred=true` when declared relationships are incomplete.
3. Use `GET /v1/database/{id}` or MCP `tablix_discover_table` only for the specific table geometry needed.

Follow `NextSkip` until `EndOfResults` is true. Treat `Source = declared_fk` as declared evidence and `Source = inferred_name_match` as a candidate until confirmed by context, schema inspection, or user approval.

### `POST /v1/database/{id}/crawl`

Re-crawl the database schema. Discovers tables, columns, primary keys, foreign keys, and indexes. The result is cached and returned.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Response** `200 OK` - returns a `DatabaseDetail` object without connection fields.

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

If the crawl itself fails (e.g. unreachable host), the response still returns `200` with `IsCrawled: false` and a `CrawlError` message.

### `POST /v1/database/{id}/crawl/stream`

Re-crawl the database schema and stream progress as server-sent events. This endpoint is used by the dashboard so users can see crawl status while the operation runs. The original `/crawl` endpoint remains available for clients that prefer a single JSON response.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Response** `200 OK`

Content type: `text/event-stream`

The stream emits `started`, multiple `progress` events, and one terminal `completed` or `failed` event. Each event's `data` field is a `CrawlProgressEvent` JSON object. Progress stages include `loading_configuration`, `discovering_schema`, `tables_discovered`, one `table_examined` event per examined table, `relationships_analyzed`, and terminal `completed` or `failed`.

```text
event: started
data: {"EventType":"started","Stage":"queued","DatabaseId":"db_sample_sqlite","Message":"Crawl request accepted.","Percent":0,"Terminal":false,"TotalMs":0}

event: progress
data: {"EventType":"progress","Stage":"table_examined","DatabaseId":"db_sample_sqlite","Message":"Examined table main.users.","Percent":42,"Terminal":false,"TotalMs":18.4,"TableName":"users","TableIndex":1,"TableCount":3,"RelationshipCount":0}

event: progress
data: {"EventType":"progress","Stage":"relationships_analyzed","DatabaseId":"db_sample_sqlite","Message":"Analyzed declared relationships.","Percent":92,"Terminal":false,"TotalMs":38.7,"TableCount":3,"RelationshipCount":2}

event: completed
data: {"EventType":"completed","Stage":"completed","DatabaseId":"db_sample_sqlite","Message":"Crawl completed.","Percent":100,"Terminal":true,"TotalMs":42.1,"TableCount":3,"RelationshipCount":2,"Detail":{...}}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

If the crawl completes in degraded state, the terminal event is `failed`, `Terminal` is `true`, and `Error`/`Detail.CrawlError` describe the issue.

---

## Query Execution

### `POST /v1/database/{id}/query`

Execute a SQL query against a database.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Request Body**

```json
{
  "Query": "SELECT * FROM users LIMIT 10"
}
```

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "RowsReturned": 3,
  "TotalMs": 12.5,
  "Data": {
    "Columns": [
      { "Name": "Id", "Type": "INTEGER" },
      { "Name": "Name", "Type": "TEXT" },
      { "Name": "Email", "Type": "TEXT" }
    ],
    "Rows": [
      { "Id": 1, "Name": "Alice", "Email": "alice@example.com" },
      { "Id": 2, "Name": "Bob", "Email": "bob@example.com" },
      { "Id": 3, "Name": "Charlie", "Email": "charlie@example.com" }
    ]
  },
  "Error": null
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Query is missing or empty |
| 403 | Statement type not in the database's `AllowedQueries` list |
| 404 | Database ID not found |
| 500 | Query execution failed (e.g. syntax error, unknown column) |

**Query Validation Rules**

- Only statement types listed in the database's `AllowedQueries` are permitted
- A single trailing SQL terminator is removed before validation and execution
- Multi-statement queries with embedded or repeated semicolons are rejected
- Leading SQL comments are stripped before validation
- This is a heuristic safeguard, not a security boundary; always use database-level permissions for production safety

---

## Chat

Chat endpoints use the selected database's saved database/table context, allowed query policy, crawl state, table geometry, relationship intelligence, and ambiguity signals to build a database-aware model prompt. The default `Chat.SystemPrompt` restricts model conversation to the selected database, its structure, its contents, and their relationships. It tells the model to use database context for database-wide guidance, table context for table-specific guidance, and schema discovery as the source of truth for table names, column names, keys, indexes, and data types. It also tells the model to execute an allowed query through the available Tablix query tool when the user asks for data that can be answered from the database, rather than merely returning SQL for the user to run, and to never fabricate result rows, counts, names, dates, metrics, or other database facts. If query execution reports a bad or unknown column, missing column, or column type mismatch, the prompt tells the model to refresh schema by crawling or discovering relevant tables, then update database or table context when refreshed schema proves saved context stale. Tablix appends mandatory query-execution and no-fabrication rules to every effective chat system prompt, including provider-specific prompts. Providers are stored in `tablix.db` and executed through PolyPrompt.

Tablix uses PolyPrompt `2.0.0` native tool chat when `Chat.PromptProcessing.PreferNativeToolCalls` and the selected persisted provider's native tool settings are enabled. Tablix defines `tablix_execute_query` plus `tablix_update_database_context` and `tablix_update_table_context` when `Chat.Tools.AllowContextUpdates` is enabled. Tablix receives model-requested tool calls, validates the selected database and query or context target, executes queries through the same validator/crawler path used by `POST /v1/database/{id}/query`, persists context updates through existing context storage, appends the tool result, and asks the model for a final answer. If native tool calls are unavailable or the model does not request a tool, `Chat.PromptProcessing.FallbackWhenNativeToolNotCalled` allows Tablix to ask the model planner to classify intent and generate one permitted query only when execution is appropriate. When Tablix detects ambiguous request terms such as active, latest, revenue, status, owner, or customer, chat returns a clarification question instead of executing speculative SQL. Tool calls, verified-answer metadata, and ambiguity signals are returned in JSON responses and streamed as SSE events. For `/v1/chat/stream`, native post-tool answers, fallback post-query answers, and plain responses are emitted as token chunks from PolyPrompt streaming APIs instead of one full completed message.

### `GET /v1/chat/options`

Return databases and enabled model providers for the dashboard Chat page.

**Response** `200 OK`

```json
{
  "Enabled": true,
  "DefaultProviderId": "provider_ollama_local",
  "DefaultStreaming": true,
  "Databases": [
    {
      "Id": "db_sample_sqlite",
      "Name": "Sample E-Commerce",
      "Type": "Sqlite",
      "HasUser": false,
      "HasPassword": false,
      "Context": "Sample e-commerce database...",
      "IsCrawled": true
    }
  ],
  "Providers": [
    {
      "Id": "provider_ollama_local",
      "Name": "Local Ollama",
      "Type": "Ollama",
      "Endpoint": "http://ollama:11434",
      "Model": "gemma3:4b",
      "Enabled": true,
      "DefaultStreaming": true,
      "HasApiKey": false
    }
  ]
}
```

### `POST /v1/chat/prompt`

Preview the effective prompt Tablix prepares for chat without calling the model. The request body is the same shape as `POST /v1/chat`; unlike a real chat request, `Messages` may be empty.

**Response** `200 OK` - returns `ChatPromptPreviewResponse`.

### `POST /v1/chat`

Send a non-streaming chat request.

```json
{
  "DatabaseId": "db_sample_sqlite",
  "ProviderId": "provider_ollama_local",
  "Streaming": false,
  "Messages": [
    { "Role": "user", "Content": "Show me SQL for total revenue by month." }
  ]
}
```

**Response** `200 OK`

```json
{
  "Success": true,
  "DatabaseId": "db_sample_sqlite",
  "ProviderId": "provider_ollama_local",
  "Model": "gemma3:4b",
  "Message": "```sql\nSELECT ...\n```",
  "Telemetry": {
    "TimeToFirstTokenMs": 842,
    "TotalStreamingTimeMs": 842,
    "InputTokens": 512,
    "OutputTokens": 96,
    "TotalTokens": 608,
    "EstimatedTokens": true
  },
  "ToolCalls": [
    {
      "Id": "9f4c2a9c8c474a2eaf35f38db88c1310",
      "Name": "tablix_execute_query",
      "Arguments": "{\"DatabaseId\":\"db_sample_sqlite\",\"Query\":\"SELECT COUNT(*) AS total_users FROM users\"}",
      "Result": "{\"Success\":true,\"RowsReturned\":1,\"Data\":{\"Rows\":[{\"total_users\":5}]}}",
      "Success": true,
      "TotalMs": 12.4,
      "Phase": "native"
    }
  ],
  "VerifiedAnswer": {
    "State": "verified",
    "Summary": "Verified by SQL execution through Tablix.",
    "Sql": "SELECT COUNT(*) AS total_users FROM users",
    "ToolCallId": "9f4c2a9c8c474a2eaf35f38db88c1310",
    "RowsReturned": 1,
    "Evidence": [
      "Tablix executed one permitted SQL statement against the selected database.",
      "Rows returned: 1."
    ]
  },
  "Ambiguities": [],
  "ExecutionPath": "native_tool_calls",
  "CapabilityNotice": "Native tool calls are enabled for this provider. Tablix still validates every database query before execution."
}
```

`VerifiedAnswer.State` is one of `verified`, `partial`, `blocked`, or `ambiguous`. `verified` means Tablix executed a successful SQL query and includes the SQL and evidence. `ambiguous` means no SQL was run because clarification is required. `blocked` means Tablix could not verify a row-dependent answer. `partial` means no row-data query was required and the answer is based on schema or saved context.

### `POST /v1/chat/stream`

Send a streaming chat request. The request body is the same as `POST /v1/chat`.

**Response** `200 OK`

Content type: `text/event-stream`

```text
event: started
data: {"EventType":"started","DatabaseId":"db_sample_sqlite","ProviderId":"provider_ollama_local","Model":"gemma3:4b","Done":false}

event: tool_started
data: {"EventType":"tool_started","ToolCall":{"Id":"9f4c2a9c8c474a2eaf35f38db88c1310","Name":"tablix_execute_query","Arguments":"{\"DatabaseId\":\"db_sample_sqlite\",\"Query\":\"SELECT COUNT(*) AS total_users FROM users\"}"},"Done":false}

event: tool_completed
data: {"EventType":"tool_completed","ToolCall":{"Id":"9f4c2a9c8c474a2eaf35f38db88c1310","Name":"tablix_execute_query","Success":true,"TotalMs":12.4,"Result":"{\"Success\":true,\"RowsReturned\":1,\"Data\":{\"Rows\":[{\"total_users\":5}]}}"},"Done":false}

event: token
data: {"EventType":"token","Delta":"There are ","Done":false}

event: token
data: {"EventType":"token","Delta":"5 users.","Done":false}

event: completed
data: {"EventType":"completed","Message":"There are 5 users.","VerifiedAnswer":{"State":"verified","Summary":"Verified by SQL execution through Tablix.","Sql":"SELECT COUNT(*) AS total_users FROM users","RowsReturned":1},"Telemetry":{"TimeToFirstTokenMs":120,"TotalStreamingTimeMs":1800,"InputTokens":512,"OutputTokens":96,"TotalTokens":608,"EstimatedTokens":false},"Done":true}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body, database ID, provider ID, or user message is missing |
| 403 | Chat is disabled |
| 404 | Database or provider not found |
| 502 | Provider request failed |

---

## Settings

Settings endpoints power the dashboard Settings page. `GET /v1/settings` exposes bootstrap/server settings and persistence health. Model providers and configured databases are managed through `/v1/model` and `/v1/database`. Prompt-processing settings take effect immediately for new chat requests; persistence filename/type and listener/logging changes are annotated as restart-required when applicable.

### `GET /v1/settings`

Return form-editable settings.

**Response** `200 OK`

```json
{
  "Persistence": {
    "Type": "Sqlite",
    "Filename": "tablix.db"
  },
  "PersistenceHealth": {
    "Type": "Sqlite",
    "Filename": "tablix.db",
    "ResolvedFilename": "C:\\Code\\Tablix\\docker\\tablix.db",
    "Exists": true,
    "CanOpen": true,
    "Error": null
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
  "ApiKeys": ["tablixadmin"],
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
  "RestartRequiredPaths": ["Persistence.Type", "Persistence.Filename", "Rest.Hostname", "Rest.Port", "Rest.Ssl", "Rest.McpPort", "Logging"]
}
```

### `PUT /v1/settings`

Save updated bootstrap settings to `tablix.json` and replace the running settings object. Chat defaults, prompt-processing settings, API keys, and other in-memory reads take effect immediately. REST/MCP listener, active logging pipeline, and persistence filename/type changes are saved immediately but require server restart to affect already-initialized components; those paths are listed in `RestartRequiredPaths`.

**Request Body**

The request uses the same editable shape returned by `GET /v1/settings`, excluding read-only `PersistenceHealth` and `RestartRequiredPaths`. Model providers are not part of the settings payload; manage providers through `/v1/model`.

`Chat.PromptProcessing` fields:

| Field | Type | Description |
|-------|------|-------------|
| `Enabled` | boolean | Enable prompt processing and tool orchestration |
| `PreferNativeToolCalls` | boolean | Prefer PolyPrompt native tool calls when provider settings allow them |
| `RequireExecutionForDataRequests` | boolean | Execute permitted queries for data-answer requests |
| `AllowSqlOnlyByExplicitRequest` | boolean | Preserve SQL-only behavior when explicitly requested |
| `FallbackWhenNativeToolNotCalled` | boolean | Use server-side fallback planning when native tools are unavailable or omitted |
| `RetryAfterSchemaRefresh` | boolean | Recrawl and retry once for schema-related query errors |
| `MaxNativeToolIterations` | integer | Maximum native tool iterations |
| `MaxPlanningAttempts` | integer | Maximum fallback planning attempts |
| `PlannerTemperature` | number | Fallback planner temperature |

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing or no API keys are supplied |
