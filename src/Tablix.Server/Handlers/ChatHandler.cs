namespace Tablix.Server.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using PolyPrompt.Clients;
    using PolyPrompt.Models;
    using SyslogLogging;
    using SwiftStack.Rest;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;

    /// <summary>
    /// REST handlers for model-backed database chat.
    /// </summary>
    public class ChatHandler
    {
        #region Private-Members

        private readonly SettingsManager _SettingsManager;
        private readonly CrawlCache _CrawlCache;
        private readonly LoggingModule _Logging;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="crawlCache">Crawl cache.</param>
        /// <param name="logging">Logging module.</param>
        public ChatHandler(SettingsManager settingsManager, CrawlCache crawlCache, LoggingModule logging)
        {
            _SettingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _CrawlCache = crawlCache ?? throw new ArgumentNullException(nameof(crawlCache));
            _Logging = logging ?? new LoggingModule();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// GET /v1/chat/options - get chat database and provider options.
        /// </summary>
        public Task<object> GetOptionsAsync(AppRequest req)
        {
            TablixSettings settings = _SettingsManager.Settings;
            ChatOptionsResponse response = new ChatOptionsResponse
            {
                Enabled = settings.Chat.Enabled,
                DefaultProviderId = settings.Chat.DefaultProviderId,
                DefaultStreaming = settings.Chat.DefaultStreaming,
                Databases = settings.Databases.Select(database => DatabaseSummary.From(database, _CrawlCache.Get(database.Id))).ToList(),
                Providers = settings.Chat.Providers
                    .Where(provider => provider.Enabled)
                    .Select(provider => ModelProviderSummary.From(provider))
                    .Where(provider => provider != null)
                    .ToList()
            };

            return Task.FromResult((object)response);
        }

        /// <summary>
        /// POST /v1/database/{id}/context/build - generate and persist database context.
        /// </summary>
        public async Task<object> BuildContextAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            BuildContextRequest request = req.GetData<BuildContextRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            TablixSettings settings = _SettingsManager.Settings;
            if (!settings.Chat.Enabled)
            {
                req.Http.Response.StatusCode = 403;
                return new ApiErrorResponse(ApiErrorEnum.Forbidden, "Chat is disabled in server settings.");
            }

            DatabaseEntry database = _SettingsManager.GetDatabase(id);
            if (database == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = _CrawlCache.Get(database.Id);
            if (detail == null || !detail.IsCrawled)
            {
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, "A successful crawl is required before building context.");
            }

            string providerId = String.IsNullOrWhiteSpace(request.ProviderId) ? settings.Chat.DefaultProviderId : request.ProviderId;
            ModelProviderSettings provider = settings.Chat.Providers.FirstOrDefault(candidate => String.Equals(candidate.Id, providerId, StringComparison.OrdinalIgnoreCase));
            if (provider == null || !provider.Enabled)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + providerId + "' not found or disabled.");
            }

            string instructions = String.IsNullOrWhiteSpace(request.Prompt) ? DefaultContextBuildInstructions() : request.Prompt.Trim();
            string prompt = BuildContextPrompt(database, detail, instructions, settings.Chat.MaxContextTables);
            string systemPrompt = "You generate concise, durable database context for Tablix settings. Restrict output to the selected database, its structure, contents, and relationships. Do not include credentials, secrets, raw result rows, or speculative facts.";

            using CompletionClientBase client = CreateClient(provider);
            ChatCompletionOptions options = CreateOptions(provider, systemPrompt);
            Stopwatch stopwatch = Stopwatch.StartNew();
            ChatResponse response = await client.ChatAsync(prompt, options, req.CancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            if (!response.Success)
            {
                req.Http.Response.StatusCode = 502;
                return new ApiErrorResponse(ApiErrorEnum.InternalError, response.Error ?? "Provider context generation failed.");
            }

            string context = NormalizeGeneratedContext(response.Text);
            if (String.IsNullOrWhiteSpace(context))
            {
                req.Http.Response.StatusCode = 502;
                return new ApiErrorResponse(ApiErrorEnum.InternalError, "Provider returned empty context.");
            }

            database.Context = context;
            _SettingsManager.UpdateDatabase(database);
            detail.Context = context;

            ChatTelemetry telemetry = CreateTelemetry(
                response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds,
                response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds,
                prompt,
                context,
                null);

            return new BuildContextResponse
            {
                Success = true,
                DatabaseId = database.Id,
                ProviderId = provider.Id,
                Context = context,
                Model = response.Model,
                Telemetry = telemetry
            };
        }

        /// <summary>
        /// POST /v1/chat - non-streaming chat.
        /// </summary>
        public async Task<object> ChatAsync(AppRequest req)
        {
            ChatRequest request = req.GetData<ChatRequest>();
            ChatPreparation preparation = await PrepareChatAsync(req, request).ConfigureAwait(false);
            if (preparation.Error != null) return preparation.Error;

            using CompletionClientBase client = CreateClient(preparation.Provider);
            ChatCompletionOptions options = CreateOptions(preparation.Provider, preparation.SystemPrompt);
            Stopwatch stopwatch = Stopwatch.StartNew();
            ChatResponse response = await client.ChatAsync(preparation.Prompt, options, req.CancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            List<ChatToolCall> toolCalls = new List<ChatToolCall>();

            if (!response.Success)
            {
                req.Http.Response.StatusCode = 502;
                return new ApiErrorResponse(ApiErrorEnum.InternalError, response.Error ?? "Provider chat request failed.");
            }

            string message = response.Text;
            ChatToolExecution execution = await TryExecuteQueryFromAssistantAsync(
                preparation,
                request,
                response.Text,
                req.CancellationToken,
                null).ConfigureAwait(false);

            if (execution.ToolCall != null)
            {
                toolCalls.Add(execution.ToolCall);
                string followupPrompt = BuildToolFollowupPrompt(preparation.Prompt, response.Text, execution.ToolCall);
                ChatResponse finalResponse = await client.ChatAsync(followupPrompt, options, req.CancellationToken).ConfigureAwait(false);
                if (finalResponse.Success)
                {
                    response = finalResponse;
                    message = finalResponse.Text;
                }
                else if (!String.IsNullOrEmpty(execution.ToolCall.Error))
                {
                    message = "The query could not be executed: " + execution.ToolCall.Error;
                }
            }

            ChatTelemetry telemetry = CreateTelemetry(
                response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds,
                response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds,
                preparation.Prompt,
                message,
                null);

            return new ChatResponseResult
            {
                Success = true,
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = response.Model,
                Message = message,
                Telemetry = telemetry,
                ToolCalls = toolCalls
            };
        }

        /// <summary>
        /// POST /v1/chat/stream - streaming chat using server-sent events.
        /// </summary>
        public async Task<object> ChatStreamAsync(AppRequest req)
        {
            ChatRequest request = req.GetData<ChatRequest>();
            ChatPreparation preparation = await PrepareChatAsync(req, request).ConfigureAwait(false);
            if (preparation.Error != null) return preparation.Error;

            req.Http.Response.StatusCode = 200;
            req.Http.Response.ContentType = "text/event-stream";
            req.Http.Response.ChunkedTransfer = true;
            req.Http.Response.Headers.Add("Cache-Control", "no-cache");
            req.Http.Response.Headers.Add("X-Accel-Buffering", "no");

            await SendChatEventAsync(req, new ChatStreamEvent
            {
                EventType = "started",
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = preparation.Provider.Model
            }, false).ConfigureAwait(false);

            using CompletionClientBase client = CreateClient(preparation.Provider);
            ChatCompletionOptions options = CreateOptions(preparation.Provider, preparation.SystemPrompt);
            Stopwatch planningStopwatch = Stopwatch.StartNew();
            ChatResponse planningResponse = await client.ChatAsync(preparation.Prompt, options, req.CancellationToken).ConfigureAwait(false);
            planningStopwatch.Stop();

            if (!planningResponse.Success)
            {
                await SendChatEventAsync(req, new ChatStreamEvent
                {
                    EventType = "error",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = preparation.Provider.Model,
                    Done = true,
                    Error = planningResponse.Error ?? "Provider chat request failed."
                }, true).ConfigureAwait(false);
                return null;
            }

            ChatToolExecution execution = await TryExecuteQueryFromAssistantAsync(
                preparation,
                request,
                planningResponse.Text,
                req.CancellationToken,
                async (evt) => await SendChatEventAsync(req, evt, false).ConfigureAwait(false)).ConfigureAwait(false);

            if (execution.ToolCall == null)
            {
                await SendChatEventAsync(req, new ChatStreamEvent
                {
                    EventType = "token",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = planningResponse.Model,
                    Delta = planningResponse.Text
                }, false).ConfigureAwait(false);

                ChatTelemetry planningTelemetry = CreateTelemetry(
                    planningResponse.OverallRuntimeMs > 0 ? planningResponse.OverallRuntimeMs : planningStopwatch.ElapsedMilliseconds,
                    planningResponse.OverallRuntimeMs > 0 ? planningResponse.OverallRuntimeMs : planningStopwatch.ElapsedMilliseconds,
                    preparation.Prompt,
                    planningResponse.Text,
                    null);

                await SendChatEventAsync(req, new ChatStreamEvent
                {
                    EventType = "completed",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = planningResponse.Model,
                    Message = planningResponse.Text,
                    Telemetry = planningTelemetry,
                    Done = true
                }, true).ConfigureAwait(false);
                return null;
            }

            string followupPrompt = BuildToolFollowupPrompt(preparation.Prompt, planningResponse.Text, execution.ToolCall);
            ChatStreamingResponse response = await client.ChatStreamingAsync(followupPrompt, options, req.CancellationToken).ConfigureAwait(false);

            if (!response.Success)
            {
                await SendChatEventAsync(req, new ChatStreamEvent
                {
                    EventType = "error",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = preparation.Provider.Model,
                    Done = true,
                    Error = response.Error ?? "Provider streaming request failed."
                }, true).ConfigureAwait(false);
                return null;
            }

            StringBuilder assistant = new StringBuilder();
            ChatStreamingUsage usage = null;

            await foreach (ChatStreamingChunk chunk in response.Chunks.WithCancellation(req.CancellationToken).ConfigureAwait(false))
            {
                if (!String.IsNullOrEmpty(chunk.Text))
                {
                    assistant.Append(chunk.Text);
                    await SendChatEventAsync(req, new ChatStreamEvent
                    {
                        EventType = "token",
                        DatabaseId = preparation.Database.Id,
                        ProviderId = preparation.Provider.Id,
                        Model = chunk.Model ?? response.Model,
                        Delta = chunk.Text
                    }, false).ConfigureAwait(false);
                }

                if (chunk.Usage != null)
                    usage = chunk.Usage;
            }

            string message = assistant.ToString();
            ChatTelemetry telemetry = CreateTelemetry(
                response.TimeToFirstTokenMs >= 0 ? response.TimeToFirstTokenMs : response.OverallRuntimeMs,
                response.OverallRuntimeMs,
                preparation.Prompt,
                message,
                usage);

            await SendChatEventAsync(req, new ChatStreamEvent
            {
                EventType = "completed",
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = response.Model,
                Message = message,
                Telemetry = telemetry,
                Done = true
            }, true).ConfigureAwait(false);

            return null;
        }

        #endregion

        #region Private-Methods

        private async Task<ChatPreparation> PrepareChatAsync(AppRequest req, ChatRequest request)
        {
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required."));
            }

            if (String.IsNullOrWhiteSpace(request.DatabaseId))
            {
                req.Http.Response.StatusCode = 400;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.BadRequest, "DatabaseId is required."));
            }

            if (request.Messages == null || request.Messages.Count == 0 || !request.Messages.Any(message => String.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase)))
            {
                req.Http.Response.StatusCode = 400;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.BadRequest, "At least one user message is required."));
            }

            TablixSettings settings = _SettingsManager.Settings;
            if (!settings.Chat.Enabled)
            {
                req.Http.Response.StatusCode = 403;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.Forbidden, "Chat is disabled in server settings."));
            }

            DatabaseEntry database = _SettingsManager.GetDatabase(request.DatabaseId);
            if (database == null)
            {
                req.Http.Response.StatusCode = 404;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + request.DatabaseId + "' not found."));
            }

            string providerId = String.IsNullOrWhiteSpace(request.ProviderId) ? settings.Chat.DefaultProviderId : request.ProviderId;
            ModelProviderSettings provider = settings.Chat.Providers.FirstOrDefault(candidate => String.Equals(candidate.Id, providerId, StringComparison.OrdinalIgnoreCase));
            if (provider == null || !provider.Enabled)
            {
                req.Http.Response.StatusCode = 404;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + providerId + "' not found or disabled."));
            }

            DatabaseDetail detail = _CrawlCache.Get(database.Id);
            if (detail == null)
                detail = await _CrawlCache.CrawlOneAsync(database).ConfigureAwait(false);

            string systemPrompt = String.IsNullOrWhiteSpace(provider.SystemPrompt) ? settings.Chat.SystemPrompt : provider.SystemPrompt;
            string prompt = BuildPrompt(settings, database, detail, request.Messages);

            return new ChatPreparation
            {
                Database = database,
                Provider = provider,
                Detail = detail,
                Settings = settings,
                SystemPrompt = systemPrompt,
                Prompt = prompt
            };
        }

        private CompletionClientBase CreateClient(ModelProviderSettings provider)
        {
            CompletionClientBase client;
            if (provider.Type == ModelProviderTypeEnum.Gemini)
                client = new GeminiClient(provider.Endpoint, provider.ApiKey, _Logging);
            else if (provider.Type == ModelProviderTypeEnum.Ollama)
                client = new OllamaClient(provider.Endpoint, provider.ApiKey, _Logging);
            else
                client = new OpenAiClient(provider.Endpoint, provider.ApiKey, _Logging);

            client.Model = provider.Model;
            client.TimeoutMs = provider.RequestTimeoutMs;
            if (provider.Temperature.HasValue) client.Temperature = provider.Temperature.Value;
            if (provider.TopP.HasValue) client.TopP = provider.TopP.Value;
            if (provider.MaxTokens.HasValue) client.MaxTokens = provider.MaxTokens.Value;
            return client;
        }

        private static ChatCompletionOptions CreateOptions(ModelProviderSettings provider, string systemPrompt)
        {
            ChatCompletionOptions options = new ChatCompletionOptions
            {
                SystemPrompt = systemPrompt
            };

            if (provider.Temperature.HasValue) options.Temperature = provider.Temperature.Value;
            if (provider.TopP.HasValue) options.TopP = provider.TopP.Value;
            if (provider.MaxTokens.HasValue) options.MaxTokens = provider.MaxTokens.Value;
            return options;
        }

        private static string BuildPrompt(TablixSettings settings, DatabaseEntry database, DatabaseDetail detail, List<ChatMessage> messages)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("You are answering questions about a configured Tablix database.");
            builder.AppendLine("Use the database context and schema below as authoritative metadata. Do not invent tables or columns.");
            builder.AppendLine("If the user asks for data or a database change and an allowed query can answer or perform it, use the available Tablix query execution tool instead of only describing SQL. Provide one compatible SQL statement with no semicolons and only needed columns so Tablix can execute it, then answer from the tool result.");
            builder.AppendLine("Do not tell the user they can run a query when they asked for the answer; Tablix will execute permitted SQL and provide results.");
            builder.AppendLine("If the user names a table, use that exact table when it exists; do not silently substitute a different table. If the named table is not present, explain the closest match or ask for clarification.");
            builder.AppendLine("If information is missing, say what needs to be inspected next.");
            builder.AppendLine();
            builder.AppendLine("Database:");
            builder.AppendLine("- Id: " + database.Id);
            builder.AppendLine("- Name: " + (database.Name ?? database.DatabaseName ?? database.Filename ?? database.Id));
            builder.AppendLine("- Type: " + database.Type);
            builder.AppendLine("- Schema: " + (database.Schema ?? "(default)"));
            builder.AppendLine("- Allowed queries: " + String.Join(", ", database.AllowedQueries));
            builder.AppendLine("- Crawl state: " + (detail.IsCrawled ? "crawled" : "degraded"));
            if (!String.IsNullOrWhiteSpace(detail.CrawlError))
                builder.AppendLine("- Crawl error: " + detail.CrawlError);
            builder.AppendLine();
            builder.AppendLine("Saved database context:");
            builder.AppendLine(String.IsNullOrWhiteSpace(database.Context) ? "(none)" : database.Context);
            builder.AppendLine();
            builder.AppendLine("Schema summary:");

            int tableLimit = Math.Clamp(settings.Chat.MaxContextTables, 1, 1000);
            List<TableDetail> tables = detail.Tables
                .OrderBy(table => table.SchemaName)
                .ThenBy(table => table.TableName)
                .Take(tableLimit)
                .ToList();

            foreach (TableDetail table in tables)
            {
                builder.AppendLine("- " + table.SchemaName + "." + table.TableName);
                builder.AppendLine("  Columns: " + String.Join(", ", table.Columns.Select(column => column.ColumnName + " " + column.DataType + (column.IsPrimaryKey ? " primary key" : "") + (column.IsNullable ? " nullable" : " not null"))));
                if (table.ForeignKeys.Count > 0)
                    builder.AppendLine("  Declared FKs: " + String.Join("; ", table.ForeignKeys.Select(foreignKey => foreignKey.ColumnName + " -> " + foreignKey.ReferencedTable + "." + foreignKey.ReferencedColumn)));
            }

            if (detail.Tables.Count > tableLimit)
                builder.AppendLine("Additional tables were omitted from prompt context. Ask the user to narrow the task or inspect specific tables.");

            builder.AppendLine();
            builder.AppendLine("Conversation:");
            foreach (ChatMessage message in messages)
            {
                if (String.IsNullOrWhiteSpace(message.Content)) continue;
                string role = String.IsNullOrWhiteSpace(message.Role) ? "user" : message.Role.Trim().ToLowerInvariant();
                builder.AppendLine(role + ": " + message.Content);
            }

            return builder.ToString();
        }

        private static string DefaultContextBuildInstructions()
        {
            return "Analyze the crawled schema and produce durable Tablix database context. Include the database purpose when inferable, major entities, important relationships, workflow groupings, naming conventions, and safe query guidance. Clearly label inferred relationships. Keep the result concise enough to use in future model prompts.";
        }

        private static string BuildContextPrompt(DatabaseEntry database, DatabaseDetail detail, string instructions, int maxContextTables)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Build persisted context for this selected Tablix database using only the last crawl below and the user's instructions.");
            builder.AppendLine("Output only the context text to save. Do not wrap it in markdown fences. Do not include credentials or raw data.");
            builder.AppendLine();
            builder.AppendLine("User instructions:");
            builder.AppendLine(instructions);
            builder.AppendLine();
            builder.AppendLine("Database:");
            builder.AppendLine("- Id: " + database.Id);
            builder.AppendLine("- Name: " + (database.Name ?? database.DatabaseName ?? database.Filename ?? database.Id));
            builder.AppendLine("- Type: " + database.Type);
            builder.AppendLine("- Schema: " + (database.Schema ?? detail.Schema ?? "(default)"));
            builder.AppendLine("- Allowed queries: " + String.Join(", ", database.AllowedQueries));
            builder.AppendLine();
            builder.AppendLine("Current saved context:");
            builder.AppendLine(String.IsNullOrWhiteSpace(database.Context) ? "(none)" : database.Context);
            builder.AppendLine();
            builder.AppendLine("Crawled schema:");

            int tableLimit = Math.Clamp(maxContextTables, 1, 1000);
            List<TableDetail> tables = detail.Tables
                .OrderBy(table => table.SchemaName)
                .ThenBy(table => table.TableName)
                .Take(tableLimit)
                .ToList();

            foreach (TableDetail table in tables)
            {
                builder.AppendLine("- " + table.SchemaName + "." + table.TableName);
                builder.AppendLine("  Columns: " + String.Join(", ", table.Columns.Select(column => column.ColumnName + " " + column.DataType + (column.IsPrimaryKey ? " primary key" : "") + (column.IsNullable ? " nullable" : " not null"))));
                if (table.ForeignKeys.Count > 0)
                    builder.AppendLine("  Declared FKs: " + String.Join("; ", table.ForeignKeys.Select(foreignKey => foreignKey.ColumnName + " -> " + foreignKey.ReferencedTable + "." + foreignKey.ReferencedColumn)));
                if (table.Indexes.Count > 0)
                    builder.AppendLine("  Indexes: " + String.Join("; ", table.Indexes.Select(index => index.IndexName + "(" + String.Join(", ", index.Columns) + ")" + (index.IsUnique ? " unique" : ""))));
            }

            if (detail.Tables.Count > tableLimit)
                builder.AppendLine("Additional tables omitted because Chat.MaxContextTables is " + tableLimit + ". Mention that context may be partial.");

            return builder.ToString();
        }

        private static string NormalizeGeneratedContext(string context)
        {
            if (String.IsNullOrWhiteSpace(context)) return null;

            string normalized = context.Trim();
            if (normalized.StartsWith("```", StringComparison.Ordinal))
            {
                normalized = Regex.Replace(normalized, "^```(?:markdown|text)?\\s*", String.Empty, RegexOptions.IgnoreCase);
                normalized = Regex.Replace(normalized, "\\s*```$", String.Empty);
            }

            return normalized.Trim();
        }

        private async Task<ChatToolExecution> TryExecuteQueryFromAssistantAsync(
            ChatPreparation preparation,
            ChatRequest request,
            string assistantText,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync)
        {
            string sql = ExtractSql(assistantText);
            if (String.IsNullOrWhiteSpace(sql))
                return new ChatToolExecution();

            if (!ShouldExecuteSql(request, assistantText, sql))
                return new ChatToolExecution();

            ChatToolCall toolCall = new ChatToolCall
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = "tablix_execute_query",
                Arguments = Serializer.SerializeJson(new Dictionary<string, string>
                {
                    { "DatabaseId", preparation.Database.Id },
                    { "Query", sql }
                }, false)
            };

            if (sendEventAsync != null)
            {
                await sendEventAsync(new ChatStreamEvent
                {
                    EventType = "tool_started",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = preparation.Provider.Model,
                    ToolCall = toolCall
                }).ConfigureAwait(false);
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            string validationError = QueryValidator.Validate(sql, preparation.Database.AllowedQueries);
            if (validationError != null)
            {
                stopwatch.Stop();
                toolCall.Success = false;
                toolCall.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                toolCall.Error = validationError;
                await SendToolCompletedIfNeededAsync(sendEventAsync, preparation, toolCall).ConfigureAwait(false);
                return new ChatToolExecution { ToolCall = toolCall };
            }

            try
            {
                IDatabaseCrawler crawler = CrawlerFactory.Create(preparation.Database.Type);
                QueryResult result = await crawler.ExecuteQueryAsync(preparation.Database, sql, token).ConfigureAwait(false);
                stopwatch.Stop();
                toolCall.Success = result.Success;
                toolCall.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                toolCall.Result = TruncateToolResult(Serializer.SerializeJson(result, false), preparation.Settings.Chat.Tools.MaxToolOutputCharacters);
                toolCall.Error = result.Error;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                toolCall.Success = false;
                toolCall.TotalMs = stopwatch.Elapsed.TotalMilliseconds;
                toolCall.Error = ex.Message;
            }

            await SendToolCompletedIfNeededAsync(sendEventAsync, preparation, toolCall).ConfigureAwait(false);
            return new ChatToolExecution { ToolCall = toolCall };
        }

        private static async Task SendToolCompletedIfNeededAsync(Func<ChatStreamEvent, Task> sendEventAsync, ChatPreparation preparation, ChatToolCall toolCall)
        {
            if (sendEventAsync == null) return;

            await sendEventAsync(new ChatStreamEvent
            {
                EventType = "tool_completed",
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = preparation.Provider.Model,
                ToolCall = toolCall
            }).ConfigureAwait(false);
        }

        private static string BuildToolFollowupPrompt(string originalPrompt, string assistantDraft, ChatToolCall toolCall)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(originalPrompt);
            builder.AppendLine();
            builder.AppendLine("The assistant drafted this SQL-oriented response:");
            builder.AppendLine(assistantDraft ?? String.Empty);
            builder.AppendLine();
            builder.AppendLine("Tablix executed this tool call:");
            builder.AppendLine("Tool: " + toolCall.Name);
            builder.AppendLine("Arguments: " + toolCall.Arguments);
            builder.AppendLine("Success: " + toolCall.Success);
            if (!String.IsNullOrWhiteSpace(toolCall.Error))
                builder.AppendLine("Error: " + toolCall.Error);
            if (!String.IsNullOrWhiteSpace(toolCall.Result))
                builder.AppendLine("Result: " + toolCall.Result);
            builder.AppendLine();
            builder.AppendLine("Now answer the user's latest request using the tool result. Do not say the user can run the query. If a value was returned, state the value directly. Keep useful SQL only if it helps explain the answer.");
            return builder.ToString();
        }

        private static string ExtractSql(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;

            Match fenced = Regex.Match(text, "```(?:sql)?\\s*(?<sql>[\\s\\S]*?)```", RegexOptions.IgnoreCase);
            if (fenced.Success)
                return NormalizeSql(fenced.Groups["sql"].Value);

            Match statement = Regex.Match(text, "\\b(?<sql>(SELECT|WITH|INSERT|UPDATE|DELETE)\\b[\\s\\S]*?)(?:;|$)", RegexOptions.IgnoreCase);
            if (statement.Success)
                return NormalizeSql(statement.Groups["sql"].Value);

            return null;
        }

        private static string NormalizeSql(string sql)
        {
            if (String.IsNullOrWhiteSpace(sql)) return null;

            string normalized = sql.Trim();
            int semicolon = normalized.IndexOf(';');
            if (semicolon >= 0)
            {
                string remainder = normalized.Substring(semicolon + 1).Trim();
                if (!String.IsNullOrEmpty(remainder))
                    return null;

                normalized = normalized.Substring(0, semicolon).Trim();
            }

            return String.IsNullOrWhiteSpace(normalized) ? null : normalized;
        }

        private static bool ShouldExecuteSql(ChatRequest request, string assistantText, string sql)
        {
            string userMessage = LastUserMessage(request);
            string user = userMessage.ToLowerInvariant();
            string assistant = (assistantText ?? String.Empty).ToLowerInvariant();
            string statementType = FirstStatementKeyword(sql);

            if (UserAskedOnlyForSql(user))
                return false;

            if (IsWriteStatement(statementType))
                return UserAskedForWrite(user, statementType);

            if (UserAskedForData(user))
                return true;

            return assistant.Contains("you can run") ||
                   assistant.Contains("running this") ||
                   assistant.Contains("executing this") ||
                   assistant.Contains("will return");
        }

        private static string LastUserMessage(ChatRequest request)
        {
            if (request == null || request.Messages == null) return String.Empty;

            for (int i = request.Messages.Count - 1; i >= 0; i--)
            {
                ChatMessage message = request.Messages[i];
                if (message != null && String.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                    return message.Content ?? String.Empty;
            }

            return String.Empty;
        }

        private static string FirstStatementKeyword(string sql)
        {
            if (String.IsNullOrWhiteSpace(sql)) return String.Empty;
            Match match = Regex.Match(sql, "^\\s*(?<keyword>[A-Za-z]+)", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups["keyword"].Value.ToUpperInvariant() : String.Empty;
        }

        private static bool UserAskedOnlyForSql(string user)
        {
            return user.Contains("sql only") ||
                   user.Contains("only sql") ||
                   user.Contains("show me the sql") ||
                   user.Contains("write sql") ||
                   user.Contains("write a sql") ||
                   user.Contains("generate sql") ||
                   user.Contains("give me sql") ||
                   user.Contains("give me a query") ||
                   user.Contains("what query");
        }

        private static bool UserAskedForData(string user)
        {
            return user.Contains("show me") ||
                   user.Contains("how many") ||
                   user.Contains("count") ||
                   user.Contains("list") ||
                   user.Contains("find") ||
                   user.Contains("total") ||
                   user.Contains("average") ||
                   user.Contains("latest") ||
                   user.Contains("top") ||
                   user.Contains("summarize") ||
                   user.Contains("what is") ||
                   user.Contains("what are");
        }

        private static bool UserAskedForWrite(string user, string statementType)
        {
            if (String.Equals(statementType, "INSERT", StringComparison.OrdinalIgnoreCase))
                return user.Contains("insert") || user.Contains("add") || user.Contains("create");

            if (String.Equals(statementType, "UPDATE", StringComparison.OrdinalIgnoreCase))
                return user.Contains("update") || user.Contains("change") || user.Contains("set ");

            if (String.Equals(statementType, "DELETE", StringComparison.OrdinalIgnoreCase))
                return user.Contains("delete") || user.Contains("remove");

            return false;
        }

        private static bool IsWriteStatement(string statementType)
        {
            return String.Equals(statementType, "INSERT", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(statementType, "UPDATE", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(statementType, "DELETE", StringComparison.OrdinalIgnoreCase);
        }

        private static string TruncateToolResult(string result, int maxCharacters)
        {
            if (String.IsNullOrEmpty(result)) return result;
            int limit = Math.Clamp(maxCharacters, 1000, 100000);
            if (result.Length <= limit) return result;
            return result.Substring(0, limit) + "...[truncated]";
        }

        private static ChatTelemetry CreateTelemetry(long timeToFirstTokenMs, long totalStreamingTimeMs, string prompt, string response, ChatStreamingUsage usage)
        {
            ChatTelemetry telemetry = new ChatTelemetry
            {
                TimeToFirstTokenMs = timeToFirstTokenMs >= 0 ? timeToFirstTokenMs : (long?)null,
                TotalStreamingTimeMs = totalStreamingTimeMs >= 0 ? totalStreamingTimeMs : (long?)null
            };

            if (usage != null && usage.PromptTokens.HasValue && usage.CompletionTokens.HasValue)
            {
                telemetry.InputTokens = usage.PromptTokens;
                telemetry.OutputTokens = usage.CompletionTokens;
                telemetry.TotalTokens = usage.TotalTokens ?? usage.PromptTokens.Value + usage.CompletionTokens.Value;
                telemetry.EstimatedTokens = false;
            }
            else
            {
                telemetry.InputTokens = EstimateTokens(prompt);
                telemetry.OutputTokens = EstimateTokens(response);
                telemetry.TotalTokens = telemetry.InputTokens + telemetry.OutputTokens;
                telemetry.EstimatedTokens = true;
            }

            return telemetry;
        }

        private static int EstimateTokens(string text)
        {
            if (String.IsNullOrEmpty(text)) return 0;
            return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
        }

        private static async Task SendChatEventAsync(AppRequest req, ChatStreamEvent evt, bool final)
        {
            string eventName = String.IsNullOrWhiteSpace(evt.EventType) ? "message" : evt.EventType;
            string json = Serializer.SerializeJson(evt, false);
            string frame = "event: " + eventName + "\n"
                + "data: " + json + "\n\n";
            byte[] bytes = Encoding.UTF8.GetBytes(frame);
            await req.Http.Response.SendChunk(bytes, final, req.CancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Private-Classes

        private class ChatPreparation
        {
            public DatabaseEntry Database { get; set; } = null;
            public ModelProviderSettings Provider { get; set; } = null;
            public DatabaseDetail Detail { get; set; } = null;
            public TablixSettings Settings { get; set; } = null;
            public string SystemPrompt { get; set; } = null;
            public string Prompt { get; set; } = null;
            public object Error { get; set; } = null;

            public static ChatPreparation Fail(object error)
            {
                return new ChatPreparation { Error = error };
            }
        }

        private class ChatToolExecution
        {
            public ChatToolCall ToolCall { get; set; } = null;
        }

        #endregion
    }
}
