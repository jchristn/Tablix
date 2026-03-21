# Tablix REST API

All responses are JSON (`Content-Type: application/json`). Authenticated endpoints require the `Authorization: Bearer <api-key>` header. API keys are configured in the `ApiKeys` array in `tablix.json`.

Interactive documentation is available at `/swagger` when the server is running.

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

## Health

### `GET /`

Health check. No authentication required.

**Response** `200 OK`

```json
{
  "Name": "Tablix",
  "Version": "0.1.0",
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
| `maxResults` | integer | 100 | Maximum results to return (1–1000) |
| `skip` | integer | 0 | Number of records to skip |
| `filter` | string | — | Filter by database ID or name (case-insensitive) |

**Response** `200 OK`

```json
{
  "Success": true,
  "MaxResults": 100,
  "Skip": 0,
  "TotalRecords": 2,
  "RecordsRemaining": 0,
  "EndOfResults": true,
  "TotalMs": 0.5,
  "Objects": [
    {
      "Id": "db_sample_sqlite",
      "Name": "Sample E-Commerce",
      "Type": "Sqlite",
      "Hostname": null,
      "Port": null,
      "User": null,
      "Password": null,
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

Get database details including connection settings and cached schema geometry.

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
  "User": null,
  "Password": null,
  "Filename": "./database.db",
  "AllowedQueries": ["SELECT", "INSERT", "UPDATE", "DELETE"]
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

**Response** `201 Created` — returns the created `DatabaseEntry`.

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing |
| 409 | A database with the same ID already exists |

### `PUT /v1/database/{id}`

Update an existing database entry. The crawl cache is updated to reflect changes immediately.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Request Body** — same structure as `POST /v1/database`. The `Id` field in the body is ignored; the path parameter is used.

**Response** `200 OK` — returns the updated `DatabaseEntry`.

**Errors**

| Status | Condition |
|--------|-----------|
| 400 | Request body is missing |
| 404 | Database ID not found |

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

### `POST /v1/database/{id}/crawl`

Re-crawl the database schema. Discovers tables, columns, primary keys, foreign keys, and indexes. The result is cached and returned.

**Path Parameters**

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | string | Database entry ID |

**Response** `200 OK` — returns a `DatabaseDetail` object (same structure as `GET /v1/database/{id}` but without the connection fields).

**Errors**

| Status | Condition |
|--------|-----------|
| 404 | Database ID not found |

If the crawl itself fails (e.g. unreachable host), the response still returns `200` with `IsCrawled: false` and a `CrawlError` message.

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
- This is a heuristic safeguard, not a security boundary — always use database-level permissions for production safety
