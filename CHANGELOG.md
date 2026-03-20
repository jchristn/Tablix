# Changelog

## v0.1.0 - ALPHA (2026-03-20)

Initial release.

### Features

- **Database providers**: SQLite, PostgreSQL, MySQL, SQL Server — schema crawl and query execution
- **Schema discovery**: crawl tables, columns, primary keys, foreign keys, and indexes on startup or on demand
- **REST API**: seven endpoints with Bearer token authentication and OpenAPI/Swagger documentation
- **MCP tools**: three Voltaic-based tools (`tablix_discover_databases`, `tablix_discover_database`, `tablix_execute_query`) for AI agent integration
- **MCP auto-installer**: `--install-mcp` CLI flag patches config for Claude Code, Cursor, Codex, and Gemini
- **Query validation**: per-database `AllowedQueries` enforcement with semicolon rejection and comment stripping
- **Degraded state**: non-fatal crawl failures with per-database error reporting
- **Dashboard**: React/Vite UI with light/dark mode, API key login, database CRUD, schema browser, query execution
- **Docker Compose**: prebuilt image deployment with sample SQLite database and factory reset scripts
- **Test suite**: unit and integration tests covering settings, validation, crawl, and CRUD operations
