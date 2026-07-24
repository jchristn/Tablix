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

Model provider credentials are stored in `tablix.db` and managed through the REST Models API or dashboard Models page. These provider keys are not part of the MCP tool response surface and must not be stored in database context.

Dashboard Chat uses PolyPrompt `2.0.0` native tool chat, including streaming tool-chat calls for `/v1/chat/stream`, when a selected provider is configured for native tool calls. That provider-facing tool loop is internal to REST chat and can expose `tablix_execute_query`, `tablix_update_database_context`, and `tablix_update_table_context`. MCP clients continue to use the explicit MCP tools documented here; `tablix_execute_query` follows the same validation and `AllowedQueries` enforcement used by REST chat native-tool and fallback execution, and MCP context update tools use the same persisted context records as REST chat context updates.

Do not save secrets, raw query result data, access tokens, connection strings, or passwords into database or table context with any context update tool.

## Tool Inventory

| Tool | Purpose |
|------|---------|
| `tablix_discover_databases` | List configured databases with redacted metadata, crawl state, query permissions, and saved context |
| `tablix_list_tables` | Page through compact table summaries |
| `tablix_list_relationships` | Page through compact declared and optionally inferred relationship edges |
| `tablix_discover_table` | Retrieve full geometry for one table |
| `tablix_execute_query` | Execute one SQL statement against a database |
| `tablix_get_database_context` | Read database-level context for one database, multiple databases, or a paged set |
| `tablix_get_table_context` | Read table-level context for one table, multiple tables, or a paged set |
| `tablix_update_context` | General database/table context update tool with a `scope` discriminator |
| `tablix_update_database_context` | Persist curated database-level context |
| `tablix_update_table_context` | Persist curated table-level context |
| `tablix_discover_database` | Retrieve full database geometry, optionally paged by table |
| `tablix_get_database_intelligence` | Read domain entities, inferred relationships, ambiguity signals, context quality, and optionally an agent pack |
| `tablix_get_agent_pack` | Read MCP-ready agent instructions and starter questions for one database |

## Recommended Agent Workflow

Restrict conversation to the selected database, its structure, its contents, and their relationships. Do not answer unrelated general-purpose questions through Tablix context.

1. Call `tablix_discover_databases`.
2. Select a database by `Id`.
3. Read `Context`, `AllowedQueries`, `IsCrawled`, and `CrawlError`.
4. When context quality matters, call `tablix_get_database_context` for the selected database to retrieve the current durable database-level context explicitly.
5. For unknown or large schemas, call `tablix_list_tables` with a conservative `maxResults`, such as `50`.
6. Continue paging by passing the previous response's `NextSkip` as `skip` until `EndOfResults` is `true`.
7. Call `tablix_get_database_intelligence` or `tablix_get_agent_pack` when you need a compact domain brief, context quality score, ambiguity warnings, or starter questions.
8. Call `tablix_list_relationships` the same way to collect declared foreign-key edges; set `includeInferred: true` when declared FKs are incomplete.
9. Before using specific tables, call `tablix_get_table_context` for those table IDs or names to retrieve durable table-specific guidance. Use `includeEmpty: true` when you need to know which selected tables have no table context yet.
10. Call `tablix_discover_table` for every table needed for SQL generation; table context does not replace column/key/index discovery.
11. Ask a clarifying question before executing SQL when intelligence or the user request exposes ambiguity around active, latest, revenue, status, owner, customer, or other business definitions.
12. Run `tablix_execute_query` after confirming the statement type is listed in `AllowedQueries` when the user asks for actual data, counts, lists, totals, computed answers, or an explicit database change.
13. If a query fails because of a bad or unknown column, missing column, or column type mismatch, refresh schema by re-discovering the relevant table or database before retrying.
14. Use `tablix_update_database_context` or `tablix_update_table_context` when the user explicitly asks to save context, the workflow clearly requires persisted analysis, or refreshed schema proves saved context has stale column names, stale column types, or stale relationship guidance.

Use `tablix_discover_database` only for small databases, explicit full-schema requests, or carefully paged full-geometry retrieval.

## Context Management Model

Tablix stores context records in `tablix.db` with two scopes:

- **Database context** describes the selected database as a whole: business purpose, major domains, important tables, declared relationships, inferred relationships clearly labeled as inferred, common query patterns, and global caveats.
- **Table context** describes one table: table purpose, important columns, business meanings, join paths, filters, row caveats, and table-specific query patterns.

Use the most specific context that applies. Database context is useful for overall orientation. Table context is more precise when generating SQL for known tables. Context is durable guidance, not proof: verify table and column names with `tablix_discover_table` before executing a query.

Context read tools:

- Use `tablix_get_database_context` for one or more database context records.
- Use `tablix_get_table_context` for one or more table context records.
- Use `includeEmpty: true` on `tablix_get_table_context` when you need selected crawled tables returned even if no table context exists yet.

Context write tools:

- Prefer `tablix_update_database_context` for database-level context.
- Prefer `tablix_update_table_context` for table-level context.
- `tablix_update_context` remains as a general tool and accepts `scope: "Database"` or `scope: "Table"` for compatibility and generic workflows.
- All update tools accept a single top-level update or an `updates` array for batch updates.

Only write context when the user asked you to save/update it, the workflow explicitly requires persisted analysis, or refreshed schema proves saved context is stale. Never store credentials, API keys, connection strings, raw query result rows, sensitive personal data copied from tables, or unsupported guesses.

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

### DatabaseContextRead

Returned by `tablix_get_database_context`.

| Field | Type | Description |
|-------|------|-------------|
| `DatabaseId` | string | Database entry ID |
| `Name` | string or null | Database display name |
| `Type` | string | Database engine type |
| `Context` | string or null | Saved database-level context |

### TableSummary

Returned by `tablix_list_tables`.

| Field | Type | Description |
|-------|------|-------------|
| `TableId` | string or null | Persisted table metadata ID used by REST table-context APIs |
| `SchemaName` | string or null | Schema name |
| `TableName` | string | Table name |
| `Columns` | integer | Number of discovered columns |
| `ForeignKeys` | integer | Number of declared foreign keys from the table |
| `Indexes` | integer | Number of discovered indexes |

### TableContextRead

Returned by `tablix_get_table_context` and table-context REST APIs.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | string or null | Context record ID when a persisted context row exists |
| `DatabaseId` | string | Database entry ID |
| `TableId` | string | Persisted table metadata ID |
| `SchemaName` | string or null | Schema name |
| `TableName` | string | Table name |
| `Context` | string or null | Saved table-level context, or null when `includeEmpty` returned an empty table |
| `Source` | string or null | Context source, such as `user`, `model`, or `mcp` |
| `UpdatedUtc` | string | Last update timestamp when persisted |

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
| `Source` | string | Relationship source: `declared_fk` or `inferred_name_match` |
| `Confidence` | number | Confidence from `0.0` to `1.0`; declared FKs use `1.0`, inferred candidates use lower scores |

### DatabaseIntelligenceResponse

Returned by `tablix_get_database_intelligence`.

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether the response succeeded |
| `DatabaseId` | string | Database entry ID |
| `Domain` | object | Schema-to-domain summary, entities, workflows, metrics, filters, and freshness columns |
| `Relationships` | `RelationshipDetail[]` | Declared and inferred relationship candidates |
| `Ambiguities` | object[] | Terms that should be clarified before executing SQL |
| `ContextQuality` | object | Score, label, coverage counts, relationship counts, and improvement signals |
| `AgentPack` | object or null | Markdown agent pack when requested |
| `TotalMs` | number | Server-side elapsed time |

### AgentPackResponse

Returned by `tablix_get_agent_pack` and optionally by `tablix_get_database_intelligence`.

| Field | Type | Description |
|-------|------|-------------|
| `Success` | boolean | Whether the response succeeded |
| `DatabaseId` | string | Database entry ID |
| `GeneratedUtc` | string | Generation timestamp |
| `Markdown` | string | MCP-ready agent brief |
| `Instructions` | string[] | Short instruction bullets |
| `SuggestedQuestions` | string[] | Useful starter questions generated from schema |

### TableDetail

Returned by `tablix_discover_table` and `tablix_discover_database`.

| Field | Type | Description |
|-------|------|-------------|
| `TableId` | string or null | Persisted table metadata ID |
| `TableName` | string | Table name |
| `SchemaName` | string or null | Schema name |
| `Context` | string or null | Persisted table-level context, when present |
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
      "TableId": "tbl_db_orders_public_invoice",
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

Lists compact relationship edges. Declared foreign keys are always returned. Set `includeInferred` to include name-based inferred relationship candidates.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `maxResults` | integer | No | `100` | Maximum relationship edges to return, clamped to `1-1000` |
| `skip` | integer | No | `0` | Number of relationship edges to skip |
| `filter` | string | No | null | Case-insensitive filter by table, column, schema, or constraint name |
| `schema` | string | No | null | Case-insensitive source or target schema filter |
| `includeInferred` | boolean | No | `false` | Include name-based inferred relationship candidates with confidence scores |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "maxResults": 100,
  "skip": 0,
  "filter": "customer",
  "includeInferred": true
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
  "IncludeInferred": true,
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 2,
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
    },
    {
      "FromSchema": "public",
      "FromTable": "orders",
      "FromColumn": "customer_id",
      "ToSchema": "public",
      "ToTable": "customers",
      "ToColumn": "id",
      "Source": "inferred_name_match",
      "Confidence": 0.90
    }
  ]
}
```

#### Guidance

- Absence of a declared edge means no declared foreign key was discovered; it does not prove tables are unrelated.
- Treat `Source = inferred_name_match` as a candidate until saved context, schema inspection, or a user confirms it.
- Clearly label inferred relationships in answers and saved context.
- Continue paging with `NextSkip` until `EndOfResults` is true.

### `tablix_get_database_intelligence`

Returns domain entities, relationship candidates, ambiguity signals, context quality, and optionally an agent pack for one database.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `includeAgentPack` | boolean | No | `true` | Include markdown agent pack |

#### Example Request

```json
{
  "databaseId": "db_orders",
  "includeAgentPack": true
}
```

#### Response

Returns `DatabaseIntelligenceResponse`.

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "Domain": {
    "Summary": "Crawled 120 table(s), 84 declared relationship(s), and 36 inferred relationship candidate(s). Saved database context is available.",
    "Entities": [
      {
        "TableId": "tbl_db_orders_public_orders",
        "SchemaName": "public",
        "TableName": "orders",
        "Role": "entity",
        "Summary": "Contains 18 column(s), 2 declared FK(s), saved context.",
        "KeyColumns": ["Id", "CustomerId", "Status", "CreatedAt"],
        "HasContext": true
      }
    ],
    "Metrics": ["public.orders.TotalAmount"],
    "CommonFilters": ["public.orders.Status", "public.orders.CreatedAt"],
    "FreshnessColumns": ["public.orders.CreatedAt"]
  },
  "Ambiguities": [
    {
      "Term": "latest",
      "Reason": "Multiple timestamp columns could define latest records.",
      "Question": "Which timestamp defines latest?",
      "Candidates": ["public.orders.CreatedAt", "public.orders.UpdatedAt"]
    }
  ],
  "ContextQuality": {
    "Score": 78,
    "Label": "Good",
    "TablesWithContext": 72,
    "TotalTables": 120,
    "DeclaredRelationships": 84,
    "InferredRelationships": 36
  }
}
```

#### Guidance

- Use this tool before generating SQL when a compact domain readout is more useful than raw schema.
- Clarify ambiguity signals before executing SQL that depends on the ambiguous term.
- Treat inferred relationship candidates as guidance, not proof, until confirmed.

### `tablix_get_agent_pack`

Returns MCP-ready instructions and starter questions for one database.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |

#### Example Request

```json
{
  "databaseId": "db_orders"
}
```

#### Response

Returns `AgentPackResponse`.

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "Markdown": "# Tablix Agent Pack: Orders\n...",
  "Instructions": [
    "Start with tablix_discover_databases and select databaseId db_orders."
  ],
  "SuggestedQuestions": [
    "How many records are in public.orders?"
  ]
}
```

#### Guidance

- Use the agent pack as a brief, not a substitute for table geometry validation.
- Preserve the selected database ID and allowed query rules when executing SQL.

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
    "TableId": "tbl_db_orders_public_orders",
    "TableName": "orders",
    "SchemaName": "public",
    "Context": "Stores customer orders and links to customers through CustomerId.",
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
| `query` | string | Yes | n/a | Single SQL statement; do not include semicolons or a trailing SQL terminator |

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
- A single trailing SQL terminator is removed before validation and execution.
- Multi-statement queries with embedded or repeated semicolons are rejected.
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
- If refreshed schema proves saved database context has wrong column names, wrong column types, or stale relationship guidance, call `tablix_update_database_context` with corrected context.
- If refreshed schema proves saved table context is stale for one or more specific tables, call `tablix_update_table_context` with corrected table-level context.

### `tablix_get_database_context`

Reads database-level context for one database, multiple databases, or a paged set of configured databases.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | No | null | Single database entry ID |
| `databaseIds` | string[] | No | `[]` | Multiple database entry IDs |
| `maxResults` | integer | No | `100` | Maximum contexts to return when listing |
| `skip` | integer | No | `0` | Records to skip when listing |
| `filter` | string | No | null | Case-insensitive filter by database ID or name |

If `databaseId` or `databaseIds` is supplied, Tablix returns the requested databases and reports missing IDs in `MissingDatabaseIds`. If neither is supplied, Tablix returns a paged list.

#### Example Request

```json
{
  "databaseIds": ["db_orders", "db_billing"]
}
```

#### Response

```json
{
  "Success": true,
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 2,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "NextSkip": null,
  "TotalMs": 0.4,
  "MissingDatabaseIds": [],
  "Error": null,
  "Objects": [
    {
      "DatabaseId": "db_orders",
      "Name": "Orders",
      "Type": "Postgresql",
      "Context": "Orders database for reporting."
    }
  ]
}
```

#### Guidance

- Use before SQL generation when durable database-level business context may affect table choice, relationship interpretation, or answer wording.
- Treat context as guidance, not proof. Verify table and column names with schema tools.
- Use `tablix_update_database_context` if refreshed schema proves database-level context is stale.

### `tablix_get_table_context`

Reads table-level context for one table, multiple tables, or a paged set of table contexts in one database.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes | n/a | Database entry ID |
| `tableId` | string | No | null | Single persisted table metadata ID |
| `tableIds` | string[] | No | `[]` | Multiple persisted table metadata IDs |
| `tableName` | string | No | null | Single table name, optionally `schema.table` |
| `tableNames` | string[] | No | `[]` | Multiple table names |
| `includeEmpty` | boolean | No | `false` | Include crawled tables without persisted table context |
| `maxResults` | integer | No | `100` | Maximum contexts to return when listing |
| `skip` | integer | No | `0` | Records to skip when listing |
| `filter` | string | No | null | Case-insensitive filter by table, schema, or context |
| `schema` | string | No | null | Exact schema filter |

If table selectors are supplied, Tablix returns those tables. If no table selectors are supplied, Tablix lists persisted table contexts. Set `includeEmpty` to `true` to list crawled tables even when their `Context` is null.

#### Example Request

```json
{
  "databaseId": "db_orders",
  "tableIds": ["tbl_db_orders_public_orders", "tbl_db_orders_public_customers"],
  "includeEmpty": true
}
```

#### Response

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 2,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "NextSkip": null,
  "TotalMs": 0.5,
  "MissingTableIds": [],
  "MissingTableNames": [],
  "Error": null,
  "Objects": [
    {
      "Id": "ctx_012345",
      "DatabaseId": "db_orders",
      "TableId": "tbl_db_orders_public_orders",
      "SchemaName": "public",
      "TableName": "orders",
      "Context": "Stores customer orders and links to customers through CustomerId.",
      "Source": "mcp",
      "UpdatedUtc": "2026-07-11T12:00:00Z"
    }
  ]
}
```

#### Guidance

- Use table context for table-specific meaning, caveats, common filters, and join guidance.
- Prefer `tableId` values from `tablix_list_tables`; use `tableName` only when IDs are not known.
- Table context does not replace schema discovery. Call `tablix_discover_table` for columns, keys, indexes, and data types before executing SQL.
- Use `tablix_update_table_context` if refreshed schema proves table-level context is stale.

### `tablix_update_database_context`

Persists database-level context for one database or multiple databases.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | No | null | Database entry ID for a single update |
| `context` | string | No | null | Context text for a single update |
| `mode` | string | No | `replace` | `replace` or `append` |
| `updates` | object[] | No | `[]` | Batch updates; each item may include `databaseId`, `context`, and `mode` |

#### Single Update

```json
{
  "databaseId": "db_orders",
  "context": "Orders database. Declared relationship: orders.CustomerId -> customers.Id.",
  "mode": "append"
}
```

#### Batch Update

```json
{
  "updates": [
    {
      "databaseId": "db_orders",
      "context": "Orders database context.",
      "mode": "replace"
    },
    {
      "databaseId": "db_billing",
      "context": "Billing database context.",
      "mode": "replace"
    }
  ]
}
```

#### Response

```json
{
  "Success": true,
  "DatabaseId": "db_orders",
  "Scope": "Database",
  "Context": "Orders database. Declared relationship: orders.CustomerId -> customers.Id.",
  "Mode": "append",
  "TotalRecords": 1,
  "Succeeded": 1,
  "Failed": 0,
  "Objects": [
    {
      "Success": true,
      "Scope": "Database",
      "DatabaseId": "db_orders",
      "Context": "Orders database. Declared relationship: orders.CustomerId -> customers.Id.",
      "Mode": "append",
      "Error": null
    }
  ],
  "Error": null
}
```

#### Guidance

- Save database context only when asked, when the workflow requires durable context, or when refreshed schema proves current database context is stale.
- Put global database guidance here; put table-specific facts in `tablix_update_table_context`.
- Preserve human-provided facts and label inferred relationships clearly.
- Do not store secrets, credentials, raw query output, sensitive table data, or guesses as facts.

### `tablix_update_table_context`

Persists table-level context for one table or multiple tables in one database.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `databaseId` | string | Yes unless supplied per item | null | Database entry ID |
| `tableId` | string | No | null | Table metadata ID for a single update |
| `tableName` | string | No | null | Table name for a single update when `tableId` is unknown |
| `context` | string | No | null | Context text for a single update |
| `mode` | string | No | `replace` | `replace` or `append` |
| `updates` | object[] | No | `[]` | Batch updates; each item may include `databaseId`, `tableId`, `tableName`, `context`, and `mode` |

#### Single Update

```json
{
  "databaseId": "db_orders",
  "tableId": "tbl_db_orders_public_orders",
  "context": "Orders table. Important columns: Id, CustomerId, OrderDate, Status, Total.",
  "mode": "replace"
}
```

#### Batch Update

```json
{
  "databaseId": "db_orders",
  "updates": [
    {
      "tableId": "tbl_db_orders_public_orders",
      "context": "Orders table context.",
      "mode": "replace"
    },
    {
      "tableName": "customers",
      "context": "Customers table context.",
      "mode": "replace"
    }
  ]
}
```

#### Response

```json
{
  "Success": true,
  "Scope": "Table",
  "DatabaseId": "db_orders",
  "TableId": "tbl_db_orders_public_orders",
  "TableName": "orders",
  "Context": "Orders table. Important columns: Id, CustomerId, OrderDate, Status, Total.",
  "Mode": "replace",
  "TotalRecords": 1,
  "Succeeded": 1,
  "Failed": 0,
  "Objects": [
    {
      "Success": true,
      "Scope": "Table",
      "DatabaseId": "db_orders",
      "TableId": "tbl_db_orders_public_orders",
      "TableName": "orders",
      "Context": "Orders table. Important columns: Id, CustomerId, OrderDate, Status, Total.",
      "Mode": "replace",
      "Error": null
    }
  ],
  "Error": null
}
```

#### Guidance

- Save table context for durable table-specific facts: table purpose, important columns, business meanings, common joins, filters, row caveats, and query patterns.
- Prefer table IDs from `tablix_list_tables`.
- Use `append` for incremental notes; use `replace` for complete curated context.
- Do not store raw row data or unsupported guesses.

### `tablix_update_context`

General context update tool retained for compatibility. Prefer the explicit aliases above when possible.

#### Input

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `scope` | string | No | `Database` | `Database` or `Table` |
| `databaseId` | string | No | null | Database entry ID |
| `tableId` | string | No | null | Table metadata ID when `scope` is `Table` |
| `tableName` | string | No | null | Table name when `scope` is `Table` and `tableId` is unknown |
| `context` | string | No | null | Context text |
| `mode` | string | No | `replace` | `replace` or `append` |
| `updates` | object[] | No | `[]` | Batch updates; each item may include `scope`, `databaseId`, `tableId`, `tableName`, `context`, and `mode` |

#### Example Request

```json
{
  "scope": "Table",
  "databaseId": "db_orders",
  "tableName": "orders",
  "context": "Orders table context.",
  "mode": "append"
}
```

#### Guidance

- Use this tool when a generic workflow needs a discriminator-based update path.
- Prefer `tablix_update_database_context` and `tablix_update_table_context` for clearer model behavior.
- The same context quality and safety rules apply.

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
