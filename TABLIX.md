# Tablix Implementation Plan

> Repo: https://github.com/jchristn/Tablix
> Status: Revised draft based on review consensus

---

## Original Prompt

Draft a plan that is actionable (a dev can annotate completion/progress) in TABLIX.md. Tablix is meant to be a server platform that can connect to a database of your choosing. The backend should be written in C#. it should have a simple REST interface, written using SwiftStack (install as a NuGet package, but you can find the source at c:\code\swiftstack) with OpenAPI/swagger documentation.

Once connected to the database, on initial startup - or when asked via MCP or REST, Tablix should retrieve the configuration of all visible tables.

The database settings and user-supplied context (free form text allowing the user to describe how the tables are used, what they contain, how they related to one another) should be stored in a tablix.json file (represented as a Settings object inside of C#) with geometry similar to the following:

```json
{
  "Rest": {
    "...": "rest settings"
  },
  "Logging": {
    "...": "logging settings"
  },
  "Databases": [
    {
      "Id": "db_abc_xyz",
      "Type": "Postgres",
      "Hostname": "localhost",
      "Port": 5432,
      "User": "postgres",
      "Password": "password",
      "DatabaseName": "mydb",
      "Schema": "public",
      "AllowedQueries": [
        "SELECT",
        "INSERT",
        "UPDATE",
        "DELETE",
        "DROP"
      ],
      "Context": "This database has a series of tables holding user information and order information. The order table has a foreign key to the user table. {and more}"
    }
  ],
  "ApiKeys": [
    "tablixadmin"
  ]
}
```

JSON should always be PascalCase.

Refer to the following projects for inspiration on how to do settings, REST, MCP, database architecture, and logging:
- c:\code\assistanthub
- c:\code\partio\partio
- c:\code\committedcoaches\chronos

Required NuGet packages:
- Swiftstack (for REST interfaces)
- Voltaic (to create MCP interfaces)
- SyslogLogging (for logging)
- SerializableDataTable (to send data tables over the wire)

The following REST APIs should be available:
- POST /v1/database/{id}/crawl - re-retrieve the table list and geometry
- PUT /v1/database/{id} - update a database entry in the settings JSON file
- PUT /v1/database - add a database entry in the settings JSON file
- DELETE /v1/database - delete a database entry in the settings JSON file
- GET /v1/database/{id} - retrieve details for that database
- GET /v1/database - retrieve all database details, paginated, following the EnumerationQuery/EnumerationResult model set forth in the reference projects
- POST /v1/database/{id}/query - execute a SQL query, returning a SerializableDataTable object

All REST APIs should require Bearer token authentication using one of the API keys found in the tablix.json file.

The following MCP tools should be available:
- Discover databases - get the list of databases, again paginated following the EnumerationQuery/EnumerationResult model, should expect inputs including maxResults, skip, contextualized filters
- Discover database - by database ID, retrieve the user context and the tables and their geometry
- Execute query - by database ID and query

Style guide:
For C#: no var, no tuples, using statements instead of declarations, using statements inside the namespace blocks, XML documentation, public things named LikeThis, private things named _LikeThis, one entity per file, null check on set where appropriate and value-clamping to reasonable ranges where appropriate. Don't use JsonElement property accessors for things that should be defined types and instances

Build a rudimentary dashboard that uses REST and allows use of all of the available APIs.

Tablix should be runnable as a console application or in docker. Create a docker/ directory with compose.yaml to start both the server and the dashboard. The dashboard should be a standalone React/Vite app. Persist the tablix.json file and logs/ directory into the server. The compose.yaml should reference images jchristn77/tablix-server:v0.1.0 and tabix-ui:v0.1.0. It should not use build contexts.

All C# work goes in src/. Create a solution file, create project files for Tablix.Core (classes), Tablix.Server (server), and add them to the solution.
All dashboard work goes in dashboard/.

Create a build-server.bat and build-dashboard.bat file. Refer to the projects above for structure. Build for both image tag supplied as a CLI argument as well as latest, using cloudbuilder cloud-jchristn77-jchristn77, and automatically pushing to docker hub.

The root needs a compelling README.md, CHANGELOG.md, .gitignore, .dockerignore (wherever it is best placed). The build-*.bat should also be in root.

The server app should have a command line argument allowing for automatic installation of MCP into Claude Code, Codex, Gemini, and Cursor, depending on their availability and your ability to find the config files.

Include an example database.db (sqlite) in the docker directory, and define it in the tablix.json file. Prepopulate the database with a users table, an orders table, and some line items per order. Copy this into a directory docker/factory/ to make a gold copy, and within docker/factory/ add reset.bat and reset.sh to allow the user to reset tablix to the default state. Follow the reset.bat model in the aforementioned projects.

The user experience is, I as a user want to:
- Connect Tablix via MCP to Claude Code, Codex, etc
- Ask my agent what databases it can see
- Ask it to run queries on my behalf
- Have the agent figure out what queries to run to answer my questions
- Present findings

I will find an icon for you to use on the dashboard login page (by API key) and on the dashboard main page and as the favicon.

Please ask any clarifying questions.

---

## Review Outcome

This revised plan reflects the consensus review outcome:
- The prior draft was a strong requirements inventory.
- It was not yet a credible execution plan.
- The main fixes are to define decisions explicitly, narrow v0.1 to a reproducible slice, add acceptance criteria, and add a test strategy.

---

## Decisions And Clarifications

These decisions should be treated as approved unless the repo owner objects.

1. REST create should be `POST /v1/database`, not `PUT /v1/database`.
2. REST delete should be `DELETE /v1/database/{id}`, not body-based delete.
3. "Geometry" means discovered tables, columns, primary keys, foreign keys, and indexes.
4. v0.1 supports single-statement SQL only. Inputs containing semicolons are rejected.
5. Query validation is a v0.1 heuristic, not a security boundary. Production safety still depends on database-side permissions.
6. REST requires Bearer token auth using `ApiKeys` from `tablix.json`.
7. MCP is unauthenticated in v0.1 by design and assumed to run in a trusted local environment.
8. Initial crawl failures are non-fatal. Server startup continues and the affected database is reported as uncrawled or degraded.
9. Crawled metadata is cache/state, not the source of truth. `tablix.json` remains the persisted source of truth for configured databases and user context.
10. The dashboard cannot rely on runtime `VITE_*` environment variables when using prebuilt images. v0.1 must choose either runtime asset substitution or fetched runtime config.
11. v0.1 is SQLite-only because the repo's reproducible demo environment provisions SQLite only.
12. Postgres is the first expansion target after v0.1. MySQL and SQL Server are deferred after that.
13. .NET 10 remains the target only if SwiftStack, Voltaic, SyslogLogging, and SerializableDataTable are verified compatible in Phase 1.

---

## Scope

### v0.1 Must Have

- `Tablix.Core` and `Tablix.Server` solution structure under `src/`
- Settings model backed by PascalCase `tablix.json`
- SQLite provider and crawler
- Full REST surface with Bearer auth and OpenAPI
- Full MCP surface for discover/list/query
- In-memory metadata cache with explicit degraded-state behavior
- Basic React/Vite dashboard covering the supported API workflows
- Dockerized demo with sample SQLite database
- Factory reset scripts for the demo environment
- Basic root documentation and build scripts
- Tests covering validation, settings, auth, crawl, and a Docker smoke path

### Deferred Until After v0.1

- Postgres provider
- MySQL provider
- SQL Server provider
- Automatic MCP installation into Claude Code, Codex, Gemini, and Cursor
- Multi-arch cloud publishing automation beyond the basic build script requirement
- Dashboard theme sophistication and polish beyond a minimal usable UI

---

## Project Structure

Only project-level structure is fixed up front.

```text
/
|-- src/
|   |-- Tablix.sln
|   |-- Tablix.Core/
|   `-- Tablix.Server/
|-- dashboard/
|-- docker/
|   |-- compose.yaml
|   |-- tablix.json
|   |-- database.db
|   `-- factory/
|-- build-server.bat
|-- build-dashboard.bat
|-- README.md
|-- CHANGELOG.md
|-- .gitignore
`-- .dockerignore
```

---

## Milestone 1: Foundation

Goal: establish the repo, dependency viability, settings model, logging, and configuration plumbing before feature work.

### Work

- [x] Create `src/Tablix.sln`
- [x] Create `src/Tablix.Core/Tablix.Core.csproj`
- [x] Create `src/Tablix.Server/Tablix.Server.csproj`
- [x] Add required NuGet dependencies
- [x] Verify `.NET 10` compatibility for required packages before proceeding further
- [x] Create settings models for `Rest`, `Logging`, `Databases`, and `ApiKeys`
- [x] Implement PascalCase JSON serialization and deserialization for `tablix.json`
- [x] Implement settings load/save service
- [x] Implement logging bootstrap
- [x] Add root placeholders for `README.md`, `CHANGELOG.md`, `.gitignore`, `.dockerignore`
- [x] Add example `docker/tablix.json`

### Acceptance Criteria

- [x] Solution restores and builds successfully
- [x] Required packages are confirmed compatible with target framework, or framework change is explicitly documented before more implementation proceeds
- [x] `tablix.json` round-trips through strongly typed settings with PascalCase JSON
- [x] Server can start, load config, and initialize logging without database connectivity

### Tests

- [x] Unit tests for settings serialization/deserialization
- [x] Unit tests for value clamping and null-guard behavior in settings models

---

## Milestone 2: SQLite Vertical Slice

Goal: deliver the first complete, reproducible backend slice using the repo-managed SQLite sample database.

### Work

- [x] Implement database abstraction and SQLite provider
- [x] Implement metadata crawler for SQLite
- [x] Define geometry models: tables, columns, PKs, FKs, indexes
- [x] Implement in-memory metadata cache and degraded-state reporting
- [x] Implement REST auth using API keys from `tablix.json`
- [x] Implement REST endpoints:
  - [x] `POST /v1/database/{id}/crawl`
  - [x] `POST /v1/database`
  - [x] `PUT /v1/database/{id}`
  - [x] `DELETE /v1/database/{id}`
  - [x] `GET /v1/database/{id}`
  - [x] `GET /v1/database`
  - [x] `POST /v1/database/{id}/query`
- [x] Implement OpenAPI/Swagger exposure
- [x] Implement MCP tools:
  - [x] Discover databases
  - [x] Discover database
  - [x] Execute query
- [x] Implement query validator:
  - [x] strip leading whitespace/comments
  - [x] reject semicolons
  - [x] check normalized leading verb against `AllowedQueries`
- [x] Define error model for auth failure, validation failure, missing database, crawl failure, and query rejection
- [x] Ensure startup crawl is non-fatal per database

### Acceptance Criteria

- [x] With the sample SQLite entry configured, server starts successfully and exposes Swagger
- [x] Initial crawl populates geometry for the sample SQLite database
- [x] If crawl fails, server still starts and the database reports degraded or uncrawled status
- [x] `GET /v1/database` returns paginated results using the expected enumeration model
- [x] `GET /v1/database/{id}` returns user context plus discovered geometry
- [x] `POST /v1/database/{id}/query` returns `SerializableDataTable` for allowed single-statement queries
- [x] Query endpoint rejects multi-statement input and disallowed verbs
- [x] REST APIs reject missing or invalid Bearer tokens
- [x] MCP can list databases, fetch a specific database, and execute an allowed query against the sample SQLite DB

### Tests

- [x] Unit tests for query validation edge cases
- [x] Unit tests for pagination/enumeration behavior
- [x] Integration tests for REST auth and CRUD flow
- [x] Integration tests for SQLite crawl against the sample DB
- [x] Integration tests for query execution and error handling

---

## Milestone 3: Dashboard

Goal: provide a minimal but complete dashboard that exercises the supported REST workflows.

### Work

- [x] Create React/Vite dashboard in `dashboard/`
- [x] Implement login flow by API key
- [x] Implement pages/views for:
  - [x] listing configured databases
  - [x] viewing one database and its geometry
  - [x] adding and editing a database entry
  - [x] deleting a database entry
  - [x] triggering a crawl
  - [x] executing a query and viewing results
- [x] Choose and implement one runtime configuration approach compatible with prebuilt images:
  - [x] fetched runtime config file, or
  - [x] container entrypoint substitution
- [x] Add icon assets when provided

### Acceptance Criteria

- [x] Dashboard can authenticate with an API key and call protected REST APIs
- [x] Dashboard can complete the full SQLite flow: list, inspect, crawl, query, create, update, delete
- [x] Dashboard can locate the server URL using the chosen runtime config strategy in Docker

### Tests

- [x] Basic UI smoke test for login and database list flow
- [x] Manual verification checklist for query execution and geometry display

---

## Milestone 4: Docker And Packaging

Goal: make the v0.1 experience reproducible from the repo with sample data and reset capability.

### Work

- [x] Create `docker/database.db` with users, orders, and line items sample data
- [x] Create `docker/tablix.json` pointing to the sample SQLite DB
- [x] Create `docker/factory/` gold copies
- [x] Add `docker/factory/reset.bat`
- [x] Add `docker/factory/reset.sh`
- [x] Create `docker/compose.yaml` with server and dashboard services only, using prebuilt images
- [x] Persist `tablix.json` and `logs/` for the server
- [x] Add `build-server.bat`
- [x] Add `build-dashboard.bat`
- [x] Document image tags and publishing behavior

### Acceptance Criteria

- [x] `docker/compose.yaml` starts server and dashboard using image references only
- [x] Mounted/persisted `tablix.json` and logs survive container restart
- [x] Factory reset scripts restore the sample DB and config to the known-good state
- [x] Fresh startup from the documented Docker flow produces a working SQLite demo

### Tests

- [x] Docker compose smoke test covering server startup, dashboard startup, and sample DB availability
- [x] Manual verification of reset scripts on Windows and Unix-like shell

---

## Milestone 5: Documentation And Release Readiness

Goal: make the repo understandable and usable for a first external consumer.

### Work

- [x] Write `README.md` covering purpose, architecture, config, REST auth, MCP usage, Docker usage, and development flow
- [x] Write `CHANGELOG.md` for v0.1.0
- [x] Document security boundaries, especially query validation limits and reliance on DB permissions
- [x] Document degraded-state behavior when crawl fails
- [x] Document deferred features explicitly

### Acceptance Criteria

- [x] A new developer can run the SQLite demo from the README without tribal knowledge
- [x] The README explains the exact v0.1 security and operational assumptions
- [x] Deferred items are clearly separated from shipped functionality

---

## Expansion Milestones After v0.1

These are intentionally not part of the first shippable slice.

### Milestone 6: Postgres

- [x] Add Postgres provider and crawler
- [x] Add reproducible Postgres demo path if it is to be considered a supported out-of-box scenario
- [x] Add tests for Postgres crawl and query execution

### Milestone 7: Additional Providers

- [x] Add MySQL provider and crawler
- [x] Add SQL Server provider and crawler
- [x] Add provider-specific tests

### Milestone 8: MCP Installer And Distribution

- [x] Add optional CLI flow for MCP auto-install into supported clients
- [x] Validate config file discovery rules per client
- [x] Expand build/publish automation as needed

---

## Open Questions For Owner Confirmation

These are the only remaining questions that should be resolved before implementation starts:

1. Confirm the route corrections:
   - `POST /v1/database`
   - `DELETE /v1/database/{id}`
2. Confirm v0.1 MCP is intentionally unauthenticated and local-only.
3. Confirm whether runtime dashboard config should use fetched config or entrypoint substitution.
4. Confirm whether `.NET 10` is mandatory if one or more required packages are not yet compatible.

---

## Summary

This plan deliberately prioritizes a working, reproducible SQLite-to-REST-to-MCP-to-dashboard slice over broad provider coverage. That trade is intentional. A smaller plan with explicit decisions, tests, and acceptance criteria is more likely to ship cleanly than a broad plan that assumes unresolved details are already settled.
