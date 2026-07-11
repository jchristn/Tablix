# Getting Started with Tablix

This guide walks through a complete first run using Docker:

1. Start Tablix with Docker Compose.
2. Sign in to the dashboard.
3. Configure a model provider.
4. Configure a database.
5. Crawl the database schema.
6. Build saved database context.
7. Chat with the database.

The Docker deployment includes:

| Component | URL |
| --- | --- |
| Dashboard | http://localhost:9101 |
| REST API | http://localhost:9100 |
| Swagger UI | http://localhost:9100/swagger |
| MCP endpoint | http://localhost:9102/rpc |

The default API key is:

```text
tablixadmin
```

## Prerequisites

Install these before starting:

- Docker Desktop or Docker Engine with Docker Compose.
- A model endpoint that Tablix can reach, such as Ollama, OpenAI, Gemini, or an OpenAI-compatible endpoint.
- Optional: a database you want to connect. The Docker folder already includes a sample SQLite database.

Important Docker networking detail: Tablix Server runs inside a container. A model endpoint or database host set to `localhost` means "inside the Tablix Server container", not your workstation. On Docker Desktop, use `host.docker.internal` to reach services running on your host machine.

## Step 1: Start Tablix

From the repository root:

```bash
cd docker
docker compose up -d
```

Check container status:

```bash
docker compose ps
```

Both containers should become healthy:

- `tablix-server`
- `tablix-ui`

The UI waits for a healthy backend and then applies a 15 second startup delay. If the dashboard does not load immediately, wait a few more seconds and refresh.

## Step 2: Open the Dashboard

Open:

```text
http://localhost:9101
```

On the login page:

1. Leave the Server URL as displayed unless you are intentionally connecting to a different server.
2. Enter the API key:

   ```text
   tablixadmin
   ```

3. Click **Sign In**.

Do not test authenticated API endpoints by pasting `/v1/...` URLs directly into a browser. The browser address bar does not include the bearer token, so authenticated endpoints should return `401` there. The dashboard sends the token after sign-in.

## Step 3: Configure a Model Provider

The Chat and Build Context features require an enabled model provider.

In the dashboard:

1. Open **Settings**.
2. Scroll to **Model Providers**.
3. Either edit an existing provider or click **Add Provider**.
4. Fill in the provider fields.
5. Ensure **Enabled** is checked.
6. Click **Save Settings**.

### Option A: Ollama on Your Host Machine

If Ollama is running on your workstation:

1. Pull a model:

   ```bash
   ollama pull gpt-oss:20b
   ```

2. In Tablix **Settings**, configure a provider:

   | Field | Value |
   | --- | --- |
   | Type | `Ollama` |
   | Endpoint | `http://host.docker.internal:11434` |
   | Model | `gpt-oss:20b` |
   | API Key | leave blank |
   | Enabled | checked |

On Linux Docker Engine, `host.docker.internal` may require additional Docker host-gateway configuration. If it does not resolve, use a network-reachable hostname or IP address for the Ollama server.

### Option B: OpenAI

In Tablix **Settings**, configure a provider:

| Field | Value |
| --- | --- |
| Type | `OpenAI` |
| Endpoint | `https://api.openai.com` |
| Model | your preferred model |
| API Key | your OpenAI API key |
| Enabled | checked |

### Option C: Gemini

In Tablix **Settings**, configure a provider:

| Field | Value |
| --- | --- |
| Type | `Gemini` |
| Endpoint | `https://generativelanguage.googleapis.com` |
| Model | your preferred Gemini model |
| API Key | your Gemini API key |
| Enabled | checked |

### Option D: OpenAI-Compatible Endpoint

Use this for LM Studio, vLLM, llama.cpp server, LiteLLM, or another compatible endpoint:

| Field | Value |
| --- | --- |
| Type | `OpenAICompatible` |
| Endpoint | the base endpoint reachable from the Tablix Server container |
| Model | the served model name |
| API Key | required by your endpoint, or blank |
| Enabled | checked |

After saving, go to **Chat** and confirm your provider appears in the Provider dropdown.

## Step 4: Configure a Database

Tablix includes a sample SQLite database named **Sample E-Commerce**. You can use it immediately, or add your own.

### Use the Included Sample Database

The sample is already configured in `docker/tablix.json`:

| Field | Value |
| --- | --- |
| Id | `db_sample_sqlite` |
| Type | `Sqlite` |
| Filename | `./database.db` |
| Schema | `main` |
| Allowed Queries | `SELECT`, `INSERT`, `UPDATE`, `DELETE` |

In the dashboard:

1. Open **Databases**.
2. Click **Sample E-Commerce**.

### Add Your Own Database

In the dashboard:

1. Open **Databases**.
2. Click **Add Database**.
3. Enter a unique **ID**, such as `db_reporting`.
4. Enter a friendly **Name**.
5. Select the database **Type**.
6. Fill in connection fields.
7. Set **Allowed Queries**.
8. Add optional **Context** if you already know what the database is for.
9. Click **Create**.

Connection examples:

#### SQLite

| Field | Example |
| --- | --- |
| Type | `Sqlite` |
| Filename | `./database.db` |
| Database Name | optional display/database name |
| Schema | `main` |

The file path is resolved inside the Tablix Server container. If you want to use another SQLite file, mount it into the container in `docker/compose.yaml`.

#### PostgreSQL

| Field | Example |
| --- | --- |
| Type | `Postgresql` |
| Hostname | `host.docker.internal` or a network host |
| Port | `5432` |
| User | your database user |
| Password | your database password |
| Database Name | your database name |
| Schema | `public` |

#### MySQL

| Field | Example |
| --- | --- |
| Type | `Mysql` |
| Hostname | `host.docker.internal` or a network host |
| Port | `3306` |
| User | your database user |
| Password | your database password |
| Database Name | your database name |

#### SQL Server

| Field | Example |
| --- | --- |
| Type | `SqlServer` |
| Hostname | `host.docker.internal` or a network host |
| Port | `1433` |
| User | your database user |
| Password | your database password |
| Database Name | your database name |
| Schema | `dbo` |

### Choose Allowed Queries Carefully

`AllowedQueries` controls which SQL statement types Tablix will execute for that database.

For read-only use, configure:

```text
SELECT
```

Only add `INSERT`, `UPDATE`, or `DELETE` when you intentionally want Tablix to permit those operations.

## Step 5: Crawl the Database

Crawling discovers schema geometry:

- Tables
- Columns
- Primary keys
- Foreign keys
- Indexes

In the dashboard:

1. Open **Databases**.
2. Click the database row.
3. Click **Crawl**.
4. Watch the **Crawl Status** panel.

The status panel shows progress events, including table-level progress. When complete, the database detail page displays discovered tables, columns, keys, and indexes.

If crawl fails:

1. Check the error shown in the database detail page.
2. Confirm the hostname is reachable from inside the `tablix-server` container.
3. Confirm credentials are correct.
4. Confirm the database server allows connections from Docker.
5. Update the database entry and crawl again.

## Step 6: Build Database Context

Database context is saved in Tablix settings and used by Chat and MCP tools to understand the database more reliably. Build Context uses the selected model provider and the last successful crawl.

In the dashboard:

1. Open **Databases**.
2. Click the database row.
3. Confirm the database status is **Crawled**.
4. Click **Build Context**.
5. Select a provider.
6. Review and edit the prompt.
7. Click **Build and Save**.

The model will analyze the crawled schema and produce context containing things like:

- The likely purpose of the database.
- Major entities and workflow groupings.
- Declared relationships.
- Clearly labeled inferred relationships.
- Naming conventions.
- Safe query guidance.

After the context is saved:

1. Stay on the database detail page.
2. Find the **Context** card.
3. Review the saved context.
4. Use **Copy database context** if you want to inspect or reuse it elsewhere.

You can also edit context manually:

1. Open the database detail page.
2. Click **Edit Context**.
3. Change the text.
4. Click **Save Context**.

## Step 7: Chat with the Database

In the dashboard:

1. Open **Chat**.
2. Select a **Database**.
3. Select a **Provider**.
4. Choose whether **Streaming** is enabled.
5. Ask a question.

Example prompts for the sample database:

```text
How many users are in the users table?
```

```text
Show me the five most recent orders.
```

```text
Which tables are related to orders?
```

```text
Summarize the schema and the main join paths.
```

The chat page supports:

- Markdown rendering.
- Streaming and non-streaming responses.
- Inline tool call displays.
- Query execution for permitted query types.
- Per-message telemetry.

When the assistant executes a query, the message shows the tool call, arguments, result, runtime, and any errors.

## Step 8: Verify with the Query Page

The **Query** page is useful for direct SQL validation.

1. Open **Query**.
2. Select the database.
3. Enter a permitted SQL statement.
4. Click **Execute**.

For the sample database:

```sql
SELECT COUNT(*) AS user_count FROM users
```

The result panel can:

- Copy the full JSON result.
- Download rows as CSV.

## Step 9: Stop or Reset the Docker Deployment

To stop containers while keeping data and settings:

```bash
cd docker
docker compose down
```

To start again:

```bash
docker compose up -d
```

To reset the Docker deployment to factory defaults:

```bash
cd docker/factory
reset.bat
```

On Linux or macOS:

```bash
cd docker/factory
./reset.sh
```

Factory reset restores:

- `docker/database.db`
- `docker/tablix.json`
- Docker logs under `docker/logs`

## Common Problems

### The dashboard says "Failed to load databases"

Check:

1. Both containers are healthy:

   ```bash
   cd docker
   docker compose ps
   ```

2. You are signed in with a valid API key.
3. You have refreshed the dashboard after the containers became healthy.
4. The dashboard is using `http://localhost:9101`, not the internal container URL.

### A direct `/v1/...` browser URL returns `401`

That is expected for authenticated endpoints. The dashboard sends the API key after sign-in. Direct browser navigation does not.

To test an API endpoint manually:

```bash
curl -H "Authorization: Bearer tablixadmin" http://localhost:9100/v1/database
```

### Build Context fails

Check:

1. The database has a successful crawl.
2. Chat is enabled in Settings.
3. The selected provider is enabled.
4. The provider endpoint is reachable from the Tablix Server container.
5. The provider has a valid API key when required.

### Chat does not execute a query

Check:

1. The database has been crawled.
2. The database context is accurate.
3. The requested SQL operation is listed in `AllowedQueries`.
4. The prompt asks for data, not only for SQL text.
5. The model provider is capable enough to follow tool-use instructions.

### Chat reports a bad or unknown column

If a query fails because a column is missing, unknown, or has an unexpected type:

1. Open the database detail page.
2. Click **Crawl** to refresh schema metadata.
3. Retry the chat question.
4. If the refreshed schema shows that saved context had stale column names, stale column types, or stale relationship guidance, use **Build Context** or **Edit Context** to correct the saved context.

### The model endpoint works on the host but not from Tablix

Remember that Tablix Server runs in Docker. Use a hostname reachable from the container:

- Docker Desktop host service: `host.docker.internal`
- Another Compose service: the Compose service name
- External service: a routable hostname or IP address

## Next Steps

After the dashboard flow works:

- Read [REST_API.md](REST_API.md) for direct REST integration.
- Read [MCP_API.md](MCP_API.md) to connect AI agents through MCP.
- Read [README.md](README.md) for source builds, tests, and release details.
