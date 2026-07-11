# Changelog

## v0.2.0 - ALPHA (2026-07-11)

Large-schema discovery, persisted database context, and test infrastructure release.

This release changes Tablix from a primarily full-schema discovery surface into a workflow that can safely guide AI agents through larger databases: page through compact indexes first, inspect only the tables needed for the task, then persist curated database context when a user asks for it.

### Added

- Added paginated table listing for large schemas through REST `GET /v1/database/{id}/tables` and MCP `tablix_list_tables`
- Added compact declared relationship listing through REST `GET /v1/database/{id}/relationships` and MCP `tablix_list_relationships`
- Added `NextSkip` continuation metadata to paginated responses
- Added optional `maxTables` and `skip` parameters to MCP `tablix_discover_database`
- Added focused context updates through REST `POST /v1/database/{id}/context`
- Added streamed schema crawl progress through REST `POST /v1/database/{id}/crawl/stream` and dashboard crawl status rendering, now including table-level and relationship-analysis progress events
- Added dashboard context viewing and inline editing on database detail pages, backed by the focused context update API
- Added copyable, fixed-size database context display on database detail pages
- Added dashboard Build Context flow and REST `POST /v1/database/{id}/context/build` to generate and persist database context from the last successful crawl through a configured model provider
- Added Databases row action overflow menu with Build Context and Delete actions
- Added Query result JSON copy and CSV download controls
- Added PolyPrompt-backed REST chat APIs: `GET /v1/chat/options`, `POST /v1/chat`, and `POST /v1/chat/stream`
- Added dashboard Chat page with database/provider selectors, streaming and non-streaming responses, markdown rendering, inline query tool calls, and telemetry hover details
- Added server-side Chat query execution loop so generated permitted SQL can be executed through Tablix query validation and returned to the model for final answers
- Added redacted settings APIs: `GET /v1/settings` and `PUT /v1/settings`
- Added dashboard Settings page with structured forms for REST/MCP, API keys, logging, chat defaults, chat tools, and model providers
- Extended MCP `tablix_update_context` with `replace` and `append` modes
- Expanded MCP tool descriptions with model-facing guidance for pagination, large schemas, relationship fidelity, query safety, and context persistence
- Clarified MCP query guidance so data-answer or action requests such as counts, totals, lists, "show me", add, update, and delete should execute permitted queries instead of only returning SQL text
- Added typed `Chat` configuration with model provider templates for Ollama, OpenAI, OpenAI-compatible endpoints, and Gemini

### Changed

- Updated `Tablix.Core` and `Tablix.Server` package versions to `0.2.0`
- Updated README, REST API documentation, Docker tags, and Postman collection for v0.2.0
- Added `MCP_API.md` as a complete reference for MCP tools, schemas, examples, and agent guidance
- Updated the default chat system prompt to restrict model conversation to the selected database, its structure, contents, and relationships
- Updated dashboard runtime configuration so Docker uses `TABLIX_SERVER_URL` for the nginx proxy while the login page displays the configured server URL; local Vite can use `VITE_TABLIX_SERVER_URL` or `TABLIX_SERVER_URL`
- Added direct `SQLitePCLRaw.lib.e_sqlite3` package override to resolve the transitive vulnerability warning from 2.1.11

### Fixed

- Fixed `SyslogServer(string hostname, int port)` to validate constructor port range consistently with the `Port` property setter
- Fixed database discovery/read responses so MCP and REST no longer return configured database usernames or plaintext passwords
- Fixed Docker default and factory `tablix.json` files to include the new `Chat` settings, explicit empty `Chat.Providers[].ApiKey` placeholders, and removed a local credential-bearing database entry from the default Docker configuration
- Fixed the Docker Compose dashboard server URL to use `http://tablix-server:9100`

### Testing

- Migrated tests from `Tablix.Tests` xUnit-only tests to Touchstone
- Added `Test.Shared` as the central source of truth for test descriptors
- Added `Test.Automated`, `Test.Xunit`, and `Test.Nunit` runners
- Preserved existing settings, serialization, query validation, pagination, and SQLite crawler coverage
- Added schema projection coverage for paginated table lists and relationship edges
- Added MCP guidance coverage to prevent regressions in model-facing tool instructions
- Added credential redaction coverage for database summaries and MCP database discovery
- Added settings coverage for chat provider defaults and provider enum serialization
- Added model guard coverage for chat telemetry, chat request list handling, and provider API key redaction
- Added crawl progress event payload coverage
- Expanded shared Touchstone coverage from 53 to 137 descriptors across query validation, settings persistence, serialization, SQLite edge cases, schema projection, model guards, crawler factory, crawl cache, MCP tool behavior, and credential redaction

### Upgrade Notes

- Prefer `tablix_list_tables`, `tablix_list_relationships`, and `tablix_discover_table` for agent workflows against unknown or large schemas. Keep `tablix_discover_database` for small databases or explicit full-schema requests.
- Paginated list responses expose continuation through `NextSkip`. Continue paging until `EndOfResults` is true.
- `tablix_list_tables` returns compact summaries, not full table geometry. Agents should call `tablix_discover_table` before generating SQL against a table.
- `tablix_list_relationships` currently reports declared foreign keys. Missing edges do not prove that two tables are unrelated.
- `tablix_update_context` writes back to `tablix.json`; callers should use it only for human-approved or workflow-approved context and should not store secrets or raw query results.
- Database discovery and REST read responses are redacted: `User` and `Password` are accepted only in write requests and are represented in read responses as `HasUser` and `HasPassword`.
- Existing `tablix.json` files continue to load without `Chat`; missing chat settings are populated from code defaults when new settings files are created or serialized.

## v0.1.1 - ALPHA (2026-05-05)

Patch release.

### Changes

- Updated `Voltaic` in `Tablix.Server` from `0.1.11` to `0.2.0`
- Rebuilt and verified the existing automated test suite against the updated dependency

## v0.1.0 - ALPHA (2026-03-20)

Initial release.

### Features

- **Database providers**: SQLite, PostgreSQL, MySQL, SQL Server - schema crawl and query execution
- **Schema discovery**: crawl tables, columns, primary keys, foreign keys, and indexes on startup or on demand
- **REST API**: seven endpoints with Bearer token authentication and OpenAPI/Swagger documentation
- **MCP tools**: six Voltaic-based tools (`tablix_discover_databases`, `tablix_discover_database`, `tablix_list_tables`, `tablix_discover_table`, `tablix_execute_query`, `tablix_update_context`) for AI agent integration
- **MCP auto-installer**: `--install-mcp` CLI flag patches config for Claude Code, Cursor, Codex, and Gemini
- **Query validation**: per-database `AllowedQueries` enforcement with semicolon rejection and comment stripping
- **Degraded state**: non-fatal crawl failures with per-database error reporting
- **Dashboard**: React/Vite UI with light/dark mode, API key login, database CRUD, schema browser, query execution
- **Docker Compose**: prebuilt image deployment with sample SQLite database and factory reset scripts
- **Test suite**: unit and integration tests covering settings, validation, crawl, and CRUD operations
