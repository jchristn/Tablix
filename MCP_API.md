# Tablix MCP API

Tablix exposes an HTTP MCP server for AI agents. The MCP endpoint is:

```text
http://localhost:9102/rpc
```

The MCP host and port are configured in `tablix.json` under `Rest.Hostname` and `Rest.McpPort`.

## Security Model

The MCP server is intended to run in a trusted local or private environment. Current MCP tools do not require an API key.

Credential redaction is enforced on discovery tools:

- `User` is never returned by MCP discovery tools.
- `Password` is never returned by MCP discovery tools.
- `HasUser` indicates whether a username is configured.
- `HasPassword` indicates whether a password is configured.

Model provider credentials are configured in `tablix.json` under `Chat.Providers[].ApiKey`. These provider keys are not part of the current MCP tool response surface and must not be stored in database context.

Do not save secrets, raw query result data, access tokens, connection strings, or passwords into database context with `tablix_update_context`.

## Tool Inventory

| Tool | Purpose |
|------|---------|
| `tablix_discover_databases` | List configured databases with redacted metadata, crawl state, query permissions, and saved context |
| `tablix_list_tables` | Page through compact table summaries |
| `tablix_list_relationships` | Page through compact declared foreign-key relationship edges |
| `tablix_discover_table` | Retrieve full geometry for one table |
| `tablix_execute_query` | Execute one SQL statement against a database |
| `tablix_update_context` | Persist curated database context back to settings |
| `tablix_discover_database` | Retrieve full database geometry, optionally paged by table |

## Recommended Agent Workflow

Restrict conversation to the selected database, its structure, its contents, and their relationships. Do not answer unrelated general-purpose questions through Tablix context.

1. Call `tablix_discover_databases`.
2. Select a database by `Id`.
3. Read `Context`, `AllowedQueries`, `IsCrawled`, and `CrawlError`.
4. For unknown or large schemas, call `tablix_list_tables` with a conservative `maxResults`, such as `50`.
5. Continue paging by passing the previous response's `NextSkip` as `skip` until `EndOfResults` is `true`.
6. Call `tablix_list_relationships` the same way to collect declared foreign-key edges.
7. Call `tablix_discover_table` for every table needed for SQL generation.
8. Run `tablix_execute_query` after confirming the statement type is listed in `AllowedQueries` when the user asks for actual data, counts, lists, totals, computed answers, or an explicit database change.
9. If a query fails because of a bad or unknown column, missing column, or column type mismatch, refresh schema by re-discovering the relevant table or database before retrying.
10. Use `tablix_update_context` when the user explicitly asks to save context, the workflow clearly requires persisted analysis, or refreshed schema proves saved context has stale column names, stale column types, or stale relationship guidance.

Use `tablix_discover_database` only for small databases, explicit full-schema requests, or carefully paged full-geometry retrieval.

## Common Response Fields

### Pagination

Paginated MCP responses use these fields:

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether the request succeeded |
| `MaxResults` | integer | Page size after clamping to `1-1000` |
| `Skip` | integer | Number of records skipped |
| `TotalRecords` | integer | Total matching records before page slicing |
| `RecordsRemaining` | integer | Records remaining after this page |
| `EndOfResults` | boolean | `true` when no further page remains |
| `NextSkip` | integer or null | Use as `skip` for the next page; null on final page |
| `TotalMs` | number | Server-side elapsed time in milliseconds |
| `Objects` | array | Page objects |

### DatabaseSummary

Returned by `tablix_discover_databases`.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string | Database entry ID |
| `Name` | string or null | Human-readable display name |
| `Type` | string | `Sqlite`, `Postgresql`, `Mysql`, or `SqlServer` |
| `Hostname` | string or null | Hostname for network databases |
| `Port` | integer or null | Network database port |
| `HasUser` | boolean | Whether a username is configured |
| `HasPassword` | boolean | Whether a password is configured |
| `DatabaseName` | string or null | Database/catalog name |
| `Schema` | string or null | Configured schema |
| `Filename` | string or null | SQLite file path |
| `AllowedQueries` | string[] | Allowed SQL statement types |
| `Context` | string or null | Saved database context |
| `IsCrawled` | boolean | Whether cached schema crawl succeeded |
| `CrawlError` | string or null | Last crawl error, if any |

### TableSummary

Returned by `tablix_list_tables`.

| Field | Type | Description |
|-------|------|-------------|
| `SchemaName` | string or null | Schema name |
| `TableName` | string | Table name |
| `Columns` | integer | Number of discovered columns |
| `ForeignKeys` | integer | Number of declared foreign keys from the table |
| `Indexes` | integer | Number of discovered indexes |

### RelationshipDetail

Returned by `tablix_list_relationships`.

| Field | Type | Description |
|-------|------|-------------|
| `FromSchema` | string or null | Source schema |
| `FromTable` | string | Source table |
| `FromColumn` | string | Source column |
| `ToSchema` | string or null | Referenced schema, when known |
| `ToTable` | string | Referenced table |
| `ToColumn` | string | Referenced column |
| `ConstraintName` | string or null | Foreign-key constraint name |
| `Source` | string | Relationship source; currently `declared_fk` |
| `Confidence` | number | Confidence from `0.0` to `1.0`; declared FKs use `1.0` |

### TableDetail

Returned by `tablix_discover_table` and `tablix_discover_database`.

| Field | Type | Description |
|-------|------|-------------|
| `TableName` | string | Table name |
| `SchemaName` | string or null | Schema name |
| `Columns` | `ColumnDetail[]` | Column geometry |
| `ForeignKeys` | `ForeignKeyDetail[]` | Declared foreign keys |
| `Indexes` | `IndexDetail[]` | Index metadata |

`ColumnDetail` fields: `ColumnName`, `DataType`, `IsNullable`, `IsPrimaryKey`, `DefaultValue`, `MaxLength`.

`ForeignKeyDetail` fields: `ConstraintName`, `ColumnName`, `ReferencedTable`, `ReferencedColumn`.

`IndexDetail` fields: `IndexName`, `Columns`, `IsUnique`.

### QueryResult

Returned by `tablix_execute_query`.

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether execution succeeded |
| `DatabaseId` | string or null | Database entry ID |
| `RowsReturned` | integer | Number of rows returned |
| `TotalMs` | number | Execution time in milliseconds |
| `Data` | object or null | Serializable data table with `Columns` and `Rows` |
| `Error` | string or null | Error message when execution fails |

## Tool Reference

### `tablix_discover_databases`

Use first in every Tablix workflow. Lists configured databases with redacted metadata, query permissions, crawl state, and saved context.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `maxResults` | integer | No | `100` | Maximum databases to return, clamped to `1-1000` |
| `skip` | integer | No | `0` | Number of database records to skip |
| `filter` | string | No | null | Case-insensitive filter for database `Id` or `DatabaseName` |

#### Example Request

```json
{
  "maxResults": 50,
  "skip": 0,
  "filter": "orders"
}
```

#### Response

Returns `EnumerationResult<DatabaseSummary>`.

```json
{
  "Success": true,
  "MaxResults": 50,
  "Skip": 0,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "NextSkip": null,
  "TotalMs": 0.4,
  "Objects": [
    {
      "Id": "db_orders",
      "Name": "Orders",
      "Type": "Postgresql",
      "Hostname": "pg.example.com",
      "Port": 5432,
      "HasUser": true,
      "HasPassword": true,
      "DatabaseName": "orders",
      "Schema": "public",
      "AllowedQueries": ["SELECT"],
      "Context": "Orders database for reporting.",
      "IsCrawled": true,
      "CrawlError": null
    }
  ]
}
```

#### Guidance

- Preserve `Context` as authoritative user-provided guidance.
- Check `AllowedQueries` before calling `tablix_execute_query`.
- Credentials are never returned.
- Continue paging with `NextSkip` until `EndOfResults` is true.

### `tablix_list_tables`

Preferred table-discovery tool for large databases. Returns compact table summaries, not full column geometry.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `maxResults` | integer | No | `100` | Maximum table summaries to return, clamped to `1-1000` |
| `skip` | integer | No | `0` | Number of table summaries to skip |
| `filter` | string | No | null | Case-insensitive filter by table or schema name |
| `schema` | string | No | null | Case-insensitive exact schema filter |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "maxResults": 50,
  "skip": 0,
  "filter": "invoice",
  "schema": "public"
}
```

#### Response

Returns `DatabaseTableListResult`, which extends `EnumerationResult<TableSummary>`.

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "Context": "Orders database for reporting.",
  "IsCrawled": true,
  "TableCount": 120,
  "Filter": "invoice",
  "Schema": "public",
  "MaxResults": 50,
  "Skip": 0,
  "TotalRecords": 3,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "NextSkip": null,
  "TotalMs": 0.6,
  "Objects": [
    {
      "SchemaName": "public",
      "TableName": "invoice",
      "Columns": 12,
      "ForeignKeys": 2,
      "Indexes": 4
    }
  ]
}
```

#### Guidance

- Do not generate SQL from table summaries alone unless the query only needs table names.
- Call `tablix_discover_table` for every table used in a select list, join, filter, insert, update, or delete.
- Continue paging with `NextSkip` until `EndOfResults` is true.

### `tablix_list_relationships`

Lists compact relationship edges. The current implementation returns declared foreign keys only.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `maxResults` | integer | No | `100` | Maximum relationship edges to return, clamped to `1-1000` |
| `skip` | integer | No | `0` | Number of relationship edges to skip |
| `filter` | string | No | null | Case-insensitive filter by table, column, schema, or constraint name |
| `schema` | string | No | null | Case-insensitive source or target schema filter |
| `includeInferred` | boolean | No | `false` | Reserved for future inferred relationships; currently ignored |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "maxResults": 100,
  "skip": 0,
  "filter": "customer",
  "includeInferred": false
}
```

#### Response

Returns `DatabaseRelationshipListResult`, which extends `EnumerationResult<RelationshipDetail>`.

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "Context": "Orders database for reporting.",
  "IsCrawled": true,
  "TableCount": 120,
  "Filter": "customer",
  "Schema": null,
  "IncludeInferred": false,
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 1,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "NextSkip": null,
  "TotalMs": 0.5,
  "Objects": [
    {
      "FromSchema": "public",
      "FromTable": "orders",
      "FromColumn": "CustomerId",
      "ToSchema": "public",
      "ToTable": "customers",
      "ToColumn": "Id",
      "ConstraintName": "fk_orders_customers",
      "Source": "declared_fk",
      "Confidence": 1.0
    }
  ]
}
```

#### Guidance

- Absence of an edge means no declared foreign key was discovered; it does not prove tables are unrelated.
- If inferring relationships from names or business context, clearly label them as inferred in answers and saved context.
- Continue paging with `NextSkip` until `EndOfResults` is true.

### `tablix_discover_table`

Retrieves full geometry for one table.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `tableName` | string | Yes | n/a | Table name from `tablix_list_tables`; matching is case-insensitive |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "tableName": "orders"
}
```

#### Response

```json
{
  "DatabaseId": "db_orders",
  "Context": "Orders database for reporting.",
  "Table": {
    "TableName": "orders",
    "SchemaName": "public",
    "Columns": [
      {
        "ColumnName": "Id",
        "DataType": "integer",
        "IsNullable": false,
        "IsPrimaryKey": true,
        "DefaultValue": null,
        "MaxLength": null
      }
    ],
    "ForeignKeys": [
      {
        "ConstraintName": "fk_orders_customers",
        "ColumnName": "CustomerId",
        "ReferencedTable": "customers",
        "ReferencedColumn": "Id"
      }
    ],
    "Indexes": [
      {
        "IndexName": "idx_orders_customerid",
        "Columns": ["CustomerId"],
        "IsUnique": false
      }
    ]
  }
}
```

#### Errors

```json
{ "Error": "databaseId is required" }
```

```json
{ "Error": "tableName is required" }
```

```json
{ "Error": "Table 'missing' not found in database 'db_orders'" }
```

#### Guidance

- Use before writing SQL that references a table.
- Call once for each table participating in a join.
- Combine with `tablix_list_relationships` for join planning.

### `tablix_execute_query`

Executes one SQL statement against a database.

Use this tool when the user asks for an answer from the data or a requested database change, not just SQL text. Requests phrased as "show me", "how many", "count", "list", "find", "total", "average", "latest", "top", "summarize", "add", "update", or "delete" should normally result in executing a permitted query after table/column validation.

Do not merely provide SQL when the user asks for a result or action and the statement type is allowed. Execute the permitted query and report the returned value, rows, or write result.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `query` | string | Yes | n/a | Single SQL statement with no semicolons |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "query": "SELECT Id, Total FROM orders LIMIT 10"
}
```

#### Response

Returns `QueryResult`.

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "RowsReturned": 2,
  "TotalMs": 12.5,
  "Data": {
    "Columns": [
      { "Name": "Id", "Type": "System.Int64" },
      { "Name": "Total", "Type": "System.Decimal" }
    ],
    "Rows": [
      { "Id": 1, "Total": 10.5 },
      { "Id": 2, "Total": 20.0 }
    ]
  },
  "Error": null
}
```

Validation and execution failures are returned as `QueryResult` with `Success: false`.

```json
{
  "Success": false,
  "DatabaseId": "db_orders",
  "RowsReturned": 0,
  "TotalMs": 0,
  "Data": null,
  "Error": "Query type DELETE is not allowed."
}
```

#### Query Validation Rules

- `query` must not be empty.
- Queries containing semicolons are rejected.
- Leading SQL comments are stripped before statement-type detection.
- The first statement keyword must appear in the database's `AllowedQueries`.
- This validation is a heuristic safeguard, not a database security boundary.

#### Guidance

- Prefer `SELECT` for exploration.
- Use explicit column lists instead of `SELECT *` when practical.
- Add sensible limits for exploratory reads.
- Aggregate queries such as `COUNT(*)` do not need a `LIMIT`.
- Do not run write statements unless the user explicitly asks and `AllowedQueries` permits the statement type.
- Validate table and column names with `tablix_discover_table` first.
- If execution fails because of a bad or unknown column, missing column, or column type mismatch, refresh schema by re-discovering the relevant table or database before retrying.
- If refreshed schema proves saved context has wrong column names, wrong column types, or stale relationship guidance, call `tablix_update_context` with corrected context.

### `tablix_update_context`

Persists database context back to `tablix.json`.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `context` | string | Yes | n/a | Context text to save |
| `mode` | string | No | `replace` | `replace` or `append` |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "context": "Orders database. Declared relationship: orders.CustomerId -> customers.Id. Common query: recent orders by customer.",
  "mode": "append"
}
```

#### Response

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "Context": "Orders database. Declared relationship: orders.CustomerId -> customers.Id. Common query: recent orders by customer.",
  "Mode": "append"
}
```

#### Errors

```json
{ "Success": false, "Error": "databaseId is required" }
```

```json
{
  "Success": false,
  "DatabaseId": "db_orders",
  "Error": "Unsupported context update mode 'merge'"
}
```

#### Guidance

- Use when the user asks to save/update context, the workflow explicitly requires persisted analysis, or refreshed schema proves saved context has stale column names, stale column types, or stale relationship guidance.
- Preserve human-provided facts.
- Separate declared relationships from inferred relationships.
- Label inferred relationships clearly.
- Do not store secrets, credentials, raw query output, or guesses as facts.
- Prefer `append` for incremental notes.
- Use `replace` only when writing a complete curated context.

### `tablix_discover_database`

Returns database schema geometry. This can be very large.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `maxTables` | integer | No | null | Optional maximum full table geometry objects to return, clamped to `1-1000` |
| `skip` | integer | No | `0` | Number of tables to skip when `maxTables` is used |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "maxTables": 25,
  "skip": 0
}
```

#### Response Without Paging

When `maxTables` is omitted, the response is `DatabaseDetail`.

```json
{
  "DatabaseId": "db_orders",
  "Type": "Postgresql",
  "DatabaseName": "orders",
  "Schema": "public",
  "Context": "Orders database for reporting.",
  "Tables": [],
  "CrawledUtc": "2026-07-11T12:00:00.000Z",
  "IsCrawled": true,
  "CrawlError": null
}
```

#### Response With Paging

When `maxTables` is supplied, the response includes pagination metadata and a `Tables` page.

```json
{
  "DatabaseId": "db_orders",
  "Type": "Postgresql",
  "DatabaseName": "orders",
  "Schema": "public",
  "Context": "Orders database for reporting.",
  "CrawledUtc": "2026-07-11T12:00:00.000Z",
  "IsCrawled": true,
  "CrawlError": null,
  "MaxResults": 25,
  "Skip": 0,
  "TotalRecords": 120,
  "RecordsRemaining": 95,
  "EndOfResults": false,
  "NextSkip": 25,
  "Tables": []
}
```

#### Errors

```json
{ "Error": "databaseId is required" }
```

```json
{ "Error": "Database 'missing' not found" }
```

#### Guidance

- Avoid this tool for large databases unless using `maxTables`.
- Prefer `tablix_list_tables`, `tablix_list_relationships`, and `tablix_discover_table` for high-fidelity targeted work.
- If using paging, continue with `skip = NextSkip` until `EndOfResults` is true.
- Do not assume a paged response is complete unless `EndOfResults` is true.

## Error Behavior

Most MCP validation failures are returned as plain JSON objects with `Error` or as `QueryResult` with `Success: false`.

Common examples:

```json
{ "Error": "databaseId is required" }
```

```json
{ "Error": "Database 'db_missing' not found" }
```

```json
{
  "Success": false,
  "DatabaseId": "db_orders",
  "Error": "Query type DELETE is not allowed."
}
```

## Large Schema Practices

- Use compact tools first.
- Keep page sizes modest.
- Follow `NextSkip`.
- Do not ask for full-database geometry unless necessary.
- Use `filter` and `schema` whenever the user question narrows the domain.
- Gather relationships before writing joins.
- Inspect full table geometry before writing SQL.

## Context Quality Guidelines

Good saved context includes:

- Business purpose of the database.
- Important tables and their roles.
- Declared relationships, clearly identified as declared.
- Inferred relationships, clearly identified as inferred.
- Common query patterns.
- Caveats such as degraded crawl state or absent declared foreign keys.

Do not save:

- Passwords, usernames, tokens, API keys, or connection strings.
- Raw query result rows.
- Sensitive personal data copied from tables.
- Unsupported guesses phrased as facts.
