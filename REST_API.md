# Tablix REST API

All responses are JSON (`Content-Type: application/json`). Authenticated endpoints require the `Authorization: Bearer <api-key>` header. API keys for Tablix REST authentication are configured in the `ApiKeys` array in `tablix.json`.

Model provider credentials are separate from REST API keys. Provider authentication material is configured under `Chat.Providers[].ApiKey` in `tablix.json`; the Docker and factory defaults include this field as an empty string placeholder for every provider template.

Interactive documentation is available at `/swagger` when the server is running. The MCP tool contract is documented separately in [MCP_API.md](MCP_API.md).

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
| `Context` | string or null | Human-authored or curated database context |

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
| `Context` | string or null | Saved database context |
| `IsCrawled` | boolean | Whether cached schema crawl succeeded |
| `CrawlError` | string or null | Last crawl error, if any |

### DatabaseReadDetail

`DatabaseReadDetail` extends full crawl geometry with redacted settings fields. It is returned by `GET /v1/database/{id}` and never includes `User` or `Password`.

| Field | Type | Description |
|-------|------|-------------|
| `DatabaseId` | string | Database entry ID |
| `Type` | string | Database engine type |
| `DatabaseName` | string or null | Database/catalog name |
| `Schema` | string or null | Schema name |
| `Context` | string or null | Saved database context |
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

`TableSummary` fields are `SchemaName`, `TableName`, `Columns`, `ForeignKeys`, and `Indexes`.

`RelationshipDetail` fields are `FromSchema`, `FromTable`, `FromColumn`, `ToSchema`, `ToTable`, `ToColumn`, `ConstraintName`, `Source`, and `Confidence`.

### Chat Schemas

`ChatRequest` fields:

| Field | Type | Description |
|-------|------|-------------|
| `DatabaseId` | string | Database ID used for schema and context |
| `ProviderId` | string | `Chat.Providers[].Id` from settings |
| `Messages` | array | Conversation messages with `Role` and `Content` |
| `Streaming` | boolean or null | Client streaming preference; null uses settings defaults |

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
| `Context` | string or null | Generated context saved to `tablix.json` |
| `Model` | string or null | Provider model |
| `Telemetry` | `ChatTelemetry` or null | Generation telemetry |
| `Error` | string or null | Error when unsuccessful |

---

## Health

### `GET /`

Health check. No authentication required.

**Response** `200 OK`

```json
{
  "Name": "Tablix",
  "Version": "0.2.0",
  "StartTimeUtc": "2026-03-20T14:30:00.000Z",
  "Uptime": "01:23:45.678"
}
```

### `HEAD /`

Lightweight health check. No authentication required. Returns `200 OK` with no body.

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

Get database details including saved context, redacted connection settings, and cached schema geometry. `Context` is read from the current server settings entry. Credential values are never returned by read endpoints; `HasUser` and `HasPassword` only indicate whether credentials are configured.

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

List compact relationship edges for a database with pagination and optional filtering. The current implementation returns declared foreign keys; inferred relationships are reserved for future use.

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
| `includeInferred` | boolean | false | Reserved for inferred relationships; currently returns declared FKs only |

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
  "IncludeInferred": false,
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 2,
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
    }
  ]
}
```

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

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

### `POST /v1/database/{id}/context`

Update only the user-supplied context for a database. This is intended for AI-assisted schema analysis workflows where a model, optionally guided by human input, persists a concise database summary back to `tablix.json`.

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

Generate database context with a configured chat provider, persist it to `tablix.json`, and update the running settings entry. The prompt uses the last successful schema crawl; the endpoint returns `409` if the database has not been crawled successfully.

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
2. Use `GET /v1/database/{id}/relationships` to page through declared foreign-key edges.
3. Use `GET /v1/database/{id}` or MCP `tablix_discover_table` only for the specific table geometry needed.

Follow `NextSkip` until `EndOfResults` is true. Relationship results currently represent declared foreign keys only; absent edges do not prove tables are unrelated.

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
- Multi-statement queries (containing `;`) are rejected
- Leading SQL comments are stripped before validation
- This is a heuristic safeguard, not a security boundary; always use database-level permissions for production safety

---

## Chat

Chat endpoints use the selected database's saved context, allowed query policy, crawl state, table geometry, and declared foreign keys to build a database-aware model prompt. The default `Chat.SystemPrompt` restricts model conversation to the selected database, its structure, its contents, and their relationships. It also tells the model to execute an allowed query through the available Tablix query tool when the user asks for data that can be answered from the database, rather than merely returning SQL for the user to run. If query execution reports a bad or unknown column, missing column, or column type mismatch, the prompt tells the model to refresh schema by crawling or discovering relevant tables, then update saved context when refreshed schema proves column names, column types, or relationship guidance were stale. Providers are configured under `Chat.Providers` in `tablix.json` and are executed through PolyPrompt.

When the model produces SQL for a user request that asks for actual data or an explicit database change, Tablix can execute the generated statement through the same query validator and crawler path used by `POST /v1/database/{id}/query`. The model then receives the tool result and produces a final answer grounded in returned rows or write outcome. Tool calls are returned in JSON responses and streamed as SSE events.

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
      "TotalMs": 12.4
    }
  ]
}
```

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

event: completed
data: {"EventType":"completed","Message":"There are 5 users.","Telemetry":{"TimeToFirstTokenMs":120,"TotalStreamingTimeMs":1800,"InputTokens":512,"OutputTokens":96,"TotalTokens":608,"EstimatedTokens":false},"Done":true}
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

Settings endpoints power the dashboard Settings page. `GET /v1/settings` redacts provider API keys and exposes `HasApiKey` instead. `PUT /v1/settings` accepts new provider `ApiKey` values; leave `ApiKey` empty to preserve the existing key, or set `ClearApiKey` to `true` to remove it.

### `GET /v1/settings`

Return form-editable settings.

**Response** `200 OK`

```json
{
  "Rest": {
    "Hostname": "*",
    "Port": 9100,
    "Ssl": false,
    "McpPort": 9102
  },
  "ApiKeys": ["tablixadmin"],
  "Chat": {
    "Enabled": true,
    "DefaultProviderId": "provider_ollama_local",
    "DefaultStreaming": true,
    "MaxContextTables": 100,
    "Providers": [
      {
        "Id": "provider_ollama_local",
        "Name": "Local Ollama",
        "Type": "Ollama",
        "Endpoint": "http://ollama:11434",
        "Model": "gemma3:4b",
        "HasApiKey": false
      }
    ]
  },
  "RestartRequiredPaths": ["Rest.Hostname", "Rest.Port", "Rest.Ssl", "Rest.McpPort"]
}
```

### `PUT /v1/settings`

Save updated settings to `tablix.json` and replace the running settings object. Chat providers, chat defaults, API keys, and other in-memory reads take effect immediately. REST/MCP listener and active logging pipeline changes are saved immediately but require server restart to affect already-initialized listeners/loggers; those paths are listed in `RestartRequiredPaths`.

**Request Body**

The request uses the same shape returned by `GET /v1/settings`, with provider objects optionally including:

| Field | Type | Description |
|-------|------|-------------|
| `ApiKey` | string or null | New provider API key; empty/null preserves existing key |
| `ClearApiKey` | boolean | Remove the existing provider API key |

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing or no API keys are supplied |
