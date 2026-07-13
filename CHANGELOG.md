# Changelog

## v0.2.0 - ALPHA (2026-07-11)

SQLite-backed product state, large-schema discovery, persisted context, dashboard chat, setup wizard, and test infrastructure release.

This release changes Tablix from a primarily full-schema discovery surface into a database-agent workspace: persist product state in `tablix.db`, guide users through first-run setup, page through compact schema indexes before inspecting table geometry, generate durable database/table context, and chat with a selected database through configured model providers.

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
- Added REST `POST /v1/database/{id}/table-context/build` and `POST /v1/database/{id}/table-context/{tableId}/build` plus dashboard and setup wizard flows for model-generated table context
- Added MCP context management tools for single and batch database/table context reads and writes: `tablix_get_database_context`, `tablix_get_table_context`, `tablix_update_database_context`, and `tablix_update_table_context`
- Added SQLite persistence in `tablix.db` for model providers, configured databases, crawl metadata, database context, table context, and setup wizard state
- Added `Persistence.Type` and `Persistence.Filename` bootstrap settings in `tablix.json`
- Added REST setup APIs: `GET /v1/setup`, `PUT /v1/setup`, `POST /v1/setup/complete`, and `POST /v1/setup/dismiss`
- Added REST model provider APIs under `/v1/model`, including saved and unsaved provider connectivity tests
- Added model provider `MaxConcurrentRequests` to bound parallel provider calls during batch operations
- Added REST database connectivity test APIs and table-context APIs under `/v1/database`
- Added dashboard Models page for provider CRUD and validation
- Added first-run setup wizard for provider validation, database validation, crawl, database context, and table context
- Added dense setup wizard table-context review grid that updates each table context editor as individual provider requests complete
- Added table-level context editing on database detail pages
- Added Docker seed `tablix.db` files for runtime and factory reset
- Added Databases row action overflow menu with Build Context and Delete actions
- Added Query result JSON copy and CSV download controls
- Added PolyPrompt-backed REST chat APIs: `GET /v1/chat/options`, `POST /v1/chat`, and `POST /v1/chat/stream`
- Upgraded PolyPrompt usage to `1.5.0` and added native tool-call orchestration for providers/models configured to support tools
- Added prompt-processing settings for native tool preference, data-request execution, SQL-only intent preservation, server fallback planning, schema-refresh retry, and planning/tool iteration limits
- Added dashboard Chat page with database/provider selectors, streaming and non-streaming responses, markdown rendering, inline query tool calls, and telemetry hover details
- Added server-side Chat fallback execution loop so permitted data requests can still execute when a model/provider does not emit a native tool call
- Added redacted settings APIs: `GET /v1/settings` and `PUT /v1/settings`
- Added dashboard Settings page with structured forms for REST/MCP, API keys, logging, persistence, chat defaults, prompt processing, and chat tools
- Extended MCP `tablix_update_context` with `replace` and `append` modes plus a `scope` discriminator for database or table context
- Expanded MCP tool descriptions with model-facing guidance for pagination, large schemas, relationship fidelity, query safety, and context persistence
- Clarified MCP query guidance so data-answer or action requests such as counts, totals, lists, "show me", add, update, and delete should execute permitted queries instead of only returning SQL text
- Added persisted model provider templates for Ollama, OpenAI, OpenAI-compatible endpoints, and Gemini
- Added provider-specific system prompt overrides to the dashboard Models workflow, allowing one model endpoint to replace the global chat prompt when explicitly configured
- Added selected-language-aware dashboard localization infrastructure and a compact language selector for English, Spanish, French, Italian, Portuguese, Mandarin, Cantonese, Kanji-labeled Japanese, Japanese, and Farsi visible text, control help, placeholders, aria labels, and tooltips

### Changed

- Updated `Tablix.Core` and `Tablix.Server` package versions to `0.2.0`
- Updated README, REST API documentation, Docker tags, and Postman collection for v0.2.0
- Added `MCP_API.md` as a complete reference for MCP tools, schemas, examples, and agent guidance
- Added `GETTING_STARTED.md` with a step-by-step Docker-to-chat onboarding flow
- Updated the default chat system prompt to restrict model conversation to the selected database, its structure, contents, and relationships
- Updated dashboard runtime configuration so Docker uses `TABLIX_SERVER_URL` for the nginx proxy while the login page displays the configured server URL; local Vite can use `VITE_TABLIX_SERVER_URL` or `TABLIX_SERVER_URL`
- Changed server startup so REST and MCP listeners start before initial background database crawls complete
- Added Docker Compose healthchecks for server and UI containers with a 5 second interval and 2 second timeout, made the UI depend on a healthy backend, and added a configurable 15 second UI startup delay
- Changed Docker persistence mounting to use `/data/tablix.db` backed by the host `docker/` directory, allowing the server to create and initialize the SQLite product-state database when the file is missing
- Updated chat and MCP guidance to refresh schema after bad/unknown column or column type errors and correct saved context when refreshed schema proves it stale
- Updated chat prompt guidance to tell models to execute allowed Tablix query-tool calls when the user asks for data that can be answered from the selected database
- Changed model provider defaults so `UseNativeToolCalls` is enabled automatically when `SupportsNativeToolCalls` is enabled
- Changed dashboard table views to use wider layouts and shared viewport-positioned overflow action menus
- Moved configured databases and model providers out of `tablix.json` and into `tablix.db`
- Updated Docker Compose to mount `tablix.db` as the product-state database
- Added direct `SQLitePCLRaw.lib.e_sqlite3` package override to resolve the transitive vulnerability warning from 2.1.11

### Fixed

- Fixed `SyslogServer(string hostname, int port)` to validate constructor port range consistently with the `Port` property setter
- Fixed database discovery/read responses so MCP and REST no longer return configured database usernames or plaintext passwords
- Fixed Docker default and factory settings so `tablix.json` is a bootstrap file and provider/database product state lives in `tablix.db`
- Fixed the Docker Compose dashboard server URL to use `http://tablix-server:9100`
- Fixed the Docker dashboard nginx proxy so `/v1/...` API requests preserve their full path and query string when forwarded to the Tablix server
- Fixed Chat page layout so transcript scrolling is constrained to the chat window while the composer remains visible
- Fixed Chat page empty state so the introductory copy is centered vertically and horizontally in the transcript when no messages exist
- Fixed Chat transcript scrolling after expanding or collapsing tool-call details so users can always reach the top and bottom of the conversation
- Fixed Chat page behavior so changing the selected database or provider clears the current conversation
- Fixed Dashboard topbar sizing so route content and chat scrolling cannot shrink its vertical height
- Fixed SQLite persistence consistency by disabling connection pooling, serializing reads and writes through a single operation gate, and wrapping write batches in explicit immediate transactions with rollback
- Fixed setup wizard table-context generation so model request timeouts apply per table and model calls are bounded by provider concurrency instead of sending one long UI-proxied batch request
- Fixed dashboard container startup so its generated nginx config preserves long-running API proxy timeouts for context generation
- Fixed `/v1/chat/stream` so plain responses and post-tool summaries stream PolyPrompt token chunks instead of emitting the completed assistant message as one token event
- Removed a credential-bearing local database entry from the checked-in Docker default configuration

### Testing

- Migrated tests from `Tablix.Tests` xUnit-only tests to Touchstone
- Added `Test.Shared` as the central source of truth for test descriptors
- Added `Test.Automated`, `Test.Xunit`, and `Test.Nunit` runners
- Preserved existing settings, serialization, query validation, pagination, and SQLite crawler coverage
- Added schema projection coverage for paginated table lists and relationship edges
- Added MCP guidance coverage to prevent regressions in model-facing tool instructions
- Added credential redaction coverage for database summaries and MCP database discovery
- Added settings coverage for chat provider defaults, prompt-processing defaults, provider tool capability fields, and provider enum serialization
- Added model guard coverage for chat telemetry, chat request list handling, and provider API key redaction
- Added crawl progress event payload coverage
- Expanded shared Touchstone coverage from 53 to 167 descriptors across query validation, settings persistence, serialization, SQLite edge cases, schema projection, model guards, crawler factory, crawl cache, MCP tool behavior, credential redaction, Docker dashboard proxy packaging, prompt-processing contracts, setup wizard behavior, localized dashboard tooltips, streaming chat contracts, provider prompt overrides, action menu consistency, and dashboard/server API contract checks

### Code Quality

- Enforced explicit local declarations by avoiding `var`
- Removed tuple usage in favor of named types
- Removed direct JSON DOM use such as `JsonElement`, `JsonNode`, `JsonObject`, and `JsonArray` from Tablix code and tests
- Split C# source so each file contains no more than one class, enum, interface, delegate, or struct declaration
- Enabled XML documentation file generation for Core, Server, and shared test projects

### Upgrade Notes

- Prefer `tablix_list_tables`, `tablix_list_relationships`, and `tablix_discover_table` for agent workflows against unknown or large schemas. Keep `tablix_discover_database` for small databases or explicit full-schema requests.
- Paginated list responses expose continuation through `NextSkip`. Continue paging until `EndOfResults` is true.
- `tablix_list_tables` returns compact summaries, not full table geometry. Agents should call `tablix_discover_table` before generating SQL against a table.
- `tablix_list_relationships` currently reports declared foreign keys. Missing edges do not prove that two tables are unrelated.
- `tablix_update_database_context` and `tablix_update_table_context` write back to `tablix.db`; callers should use them only for human-approved or workflow-approved context and should not store secrets or raw query results. `tablix_update_context` remains available as a generic scoped compatibility tool.
- Database discovery and REST read responses are redacted: `User` and `Password` are accepted only in write requests and are represented in read responses as `HasUser` and `HasPassword`.
- Existing pre-persistence `tablix.json` files can be imported into an empty `tablix.db`; new default JSON files no longer contain providers or configured databases.

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
