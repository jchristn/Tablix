<p align="center">
  <img src="https://raw.githubusercontent.com/jchristn/Tablix/main/assets/logo.png" alt="Tablix" height="192" />
</p>

<p align="center">
  <b>v0.1.0 - ALPHA</b> - API and structure may change without notice
</p>

<p align="center">
  <b>Authors:</b> @jchristn @rawingate
</p>

# Tablix

Tablix is a database discovery and query platform that connects your databases to AI agents and humans through REST and MCP interfaces.

## What Is Tablix?

Tablix sits between your databases and your tools. It crawls database schemas — discovering tables, columns, primary keys, foreign keys, and indexes — and exposes that metadata alongside query execution through a REST API and an MCP server. A built-in dashboard provides a browser-based UI for the same operations.

**Supported databases:** SQLite, PostgreSQL, MySQL, SQL Server.

## Why Use Tablix?

- **Give AI agents database access.** Connect Tablix via MCP to Claude Code, Cursor, Codex, or Gemini. Your agent can discover what databases are available, understand their schemas, and run queries to answer your questions — without you writing SQL.
- **Centralize database discovery.** Configure all your database connections in one place with user-supplied context that describes what each database contains and how its tables relate to one another. AI agents use this context to figure out what queries to run.
- **Control what's allowed.** Each database entry specifies which SQL statement types are permitted (`SELECT`, `INSERT`, `UPDATE`, `DELETE`, etc.). Tablix validates every query before execution.
- **Inspect schemas visually.** The dashboard shows crawled table geometry — columns, types, primary keys, foreign keys, and indexes — in a clean, browsable interface with light and dark modes.

## How It Works

1. Configure one or more database connections in `tablix.json`
2. Tablix crawls each database on startup, caching schema geometry
3. AI agents connect via MCP to discover databases and execute queries
4. Humans use the dashboard or REST API for the same operations
5. Query validation enforces the `AllowedQueries` list per database

## Getting Started

### Docker Compose

```bash
git clone https://github.com/jchristn/Tablix.git
cd Tablix/docker
docker compose up -d
```

| Service | URL |
|---------|-----|
| REST API | http://localhost:9100 |
| Swagger UI | http://localhost:9100/swagger |
| Dashboard | http://localhost:9101 |
| MCP | http://localhost:9102/rpc |

Default API key: `tablixadmin`

The Docker setup includes a sample SQLite database with `users`, `orders`, and `line_items` tables.

### From Source

```bash
cd src
dotnet build
dotnet run --project Tablix.Server
```

The server creates a default `tablix.json` on first run with a sample SQLite database entry.

### Dashboard Development

```bash
cd dashboard
npm install
npm run dev
```

### Running Tests

```bash
cd src
dotnet test
```

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

The recommended discovery flow for AI agents is:

1. **`tablix_discover_databases`** — List all configured databases to identify which ones are available
2. **`tablix_list_tables`** — List the tables in a specific database
3. **`tablix_discover_table`** — Get full geometry (columns, types, PKs, FKs, indexes) for a specific table
4. **`tablix_execute_query`** — Execute a SQL query once you understand the schema

| Tool | Description |
|------|-------------|
| `tablix_discover_databases` | List all configured databases with pagination and filtering |
| `tablix_discover_database` | Get full schema geometry for an entire database (prefer the targeted flow above) |
| `tablix_list_tables` | List table names, schemas, and column counts for a database |
| `tablix_discover_table` | Get full geometry for a single table in a database |
| `tablix_execute_query` | Execute a validated SQL query |
| `tablix_update_context` | Update the user-supplied context description for a database |

## Configuration

Tablix is configured via `tablix.json`:

```json
{
  "Rest": {
    "Hostname": "*",
    "Port": 9100,
    "Ssl": false,
    "McpPort": 9102
  },
  "Logging": {
    "ConsoleLogging": true,
    "FileLogging": true,
    "LogDirectory": "./logs/",
    "LogFilename": "tablix.log",
    "MinimumSeverity": 0,
    "EnableColors": true
  },
  "Databases": [
    {
      "Id": "db_sample_sqlite",
      "Type": "Sqlite",
      "Filename": "./database.db",
      "AllowedQueries": ["SELECT", "INSERT", "UPDATE", "DELETE"],
      "Context": "Description of the database for AI agents..."
    }
  ],
  "ApiKeys": ["tablixadmin"]
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
| `Context` | Free-form description for AI agents |

### REST API

All endpoints require `Authorization: Bearer <api-key>`.

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/database` | List databases (paginated) |
| `GET` | `/v1/database/{id}` | Get database details and schema geometry |
| `POST` | `/v1/database` | Add a database entry |
| `PUT` | `/v1/database/{id}` | Update a database entry |
| `DELETE` | `/v1/database/{id}` | Delete a database entry |
| `POST` | `/v1/database/{id}/crawl` | Re-crawl database schema |
| `POST` | `/v1/database/{id}/query` | Execute a SQL query |

### Query Validation

- Only statement types listed in `AllowedQueries` are permitted
- Multi-statement queries (containing `;`) are rejected
- Leading SQL comments are stripped before validation
- **This is a heuristic safeguard, not a security boundary** — always use database-level permissions for production safety
- Passwords in `tablix.json` are stored in cleartext — protect the file with OS-level permissions

### Degraded State

If a database crawl fails on startup (unreachable host, bad credentials, missing file):

- The server continues to start — crawl failures are non-fatal
- The affected database reports `IsCrawled: false` with a `CrawlError` message
- Re-crawl at any time via `POST /v1/database/{id}/crawl` or the dashboard
- Query execution may still work even when the crawl has not completed

### Docker Images

| Image | Docker Hub |
|-------|-----------|
| `jchristn77/tablix-server` | https://hub.docker.com/r/jchristn77/tablix-server |
| `jchristn77/tablix-ui` | https://hub.docker.com/r/jchristn77/tablix-ui |

Build and push images:

```bash
build-server.bat v0.1.0
build-dashboard.bat v0.1.0
```

### Factory Reset

To restore the Docker environment to its default state:

```bash
cd docker/factory
./reset.sh      # Linux/Mac
reset.bat       # Windows
```

## Issues and Discussions

- Report bugs and request features at https://github.com/jchristn/Tablix/issues
- Start or join discussions at https://github.com/jchristn/Tablix/discussions

## License

[MIT License](LICENSE.md) — Copyright (c) 2026 Joel Christner
