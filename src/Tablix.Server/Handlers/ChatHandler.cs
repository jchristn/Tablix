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
    using Tablix.Core.Persistence;
    using Tablix.Core.Settings;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;
    using CoreChatMessage = Tablix.Core.Models.ChatMessage;
    using PromptChatMessage = PolyPrompt.Models.ChatMessage;
    using PromptToolCall = PolyPrompt.Models.ToolCall;

    /// <summary>
    /// REST handlers for model-backed database chat.
    /// </summary>
    public class ChatHandler
    {
        #region Private-Members

        private readonly SettingsManager _SettingsManager;
        private readonly DatabaseDriverBase _Persistence;
        private readonly CrawlCache _CrawlCache;
        private readonly LoggingModule _Logging;
        private readonly ChatQueryExecutionService _QueryExecution;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="persistence">Persistence driver.</param>
        /// <param name="crawlCache">Crawl cache.</param>
        /// <param name="logging">Logging module.</param>
        public ChatHandler(SettingsManager settingsManager, DatabaseDriverBase persistence, CrawlCache crawlCache, LoggingModule logging)
        {
            _SettingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _CrawlCache = crawlCache ?? throw new ArgumentNullException(nameof(crawlCache));
            _Logging = logging ?? new LoggingModule();
            _QueryExecution = new ChatQueryExecutionService(_CrawlCache);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// GET /v1/chat/options - get chat database and provider options.
        /// </summary>
        public async Task<object> GetOptionsAsync(AppRequest req)
        {
            TablixSettings settings = _SettingsManager.Settings;
            List<DatabaseEntry> databases = await _Persistence.DatabaseConnections.EnumerateAsync(1000, 0, null, req.CancellationToken).ConfigureAwait(false);
            List<ModelProviderSettings> providers = await _Persistence.ModelProviders.EnumerateAsync(1000, 0, null, true, req.CancellationToken).ConfigureAwait(false);
            List<DatabaseSummary> databaseSummaries = new List<DatabaseSummary>();
            foreach (DatabaseEntry database in databases)
            {
                DatabaseDetail detail = _CrawlCache.Get(database.Id);
                if (detail == null)
                    detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
                databaseSummaries.Add(DatabaseSummary.From(database, detail));
            }

            ChatOptionsResponse response = new ChatOptionsResponse
            {
                Enabled = settings.Chat.Enabled,
                DefaultProviderId = settings.Chat.DefaultProviderId,
                DefaultStreaming = settings.Chat.DefaultStreaming,
                Databases = databaseSummaries,
                Providers = providers
                    .Select(provider => ModelProviderSummary.From(provider))
                    .Where(provider => provider != null)
                    .ToList()
            };

            return response;
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

            DatabaseEntry database = await _Persistence.DatabaseConnections.ReadAsync(id, req.CancellationToken).ConfigureAwait(false);
            if (database == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + id + "' not found.");
            }

            DatabaseDetail detail = _CrawlCache.Get(database.Id);
            if (detail == null || !detail.IsCrawled)
                detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
            if (detail == null || !detail.IsCrawled)
            {
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, "A successful crawl is required before building context.");
            }

            string providerId = String.IsNullOrWhiteSpace(request.ProviderId) ? settings.Chat.DefaultProviderId : request.ProviderId;
            ModelProviderSettings provider = await _Persistence.ModelProviders.ReadAsync(providerId, req.CancellationToken).ConfigureAwait(false);
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

            database.Context = await _Persistence.DatabaseContexts.UpsertAsync(database.Id, context, "replace", "model", req.CancellationToken).ConfigureAwait(false);
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
        /// POST /v1/database/{id}/table-context/build - generate and persist table context for every selected table.
        /// </summary>
        public async Task<object> BuildAllTableContextsAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            BuildTableContextRequest request = req.GetData<BuildTableContextRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            ChatPreparation preparation = await PrepareContextBuildAsync(req, id, request.ProviderId).ConfigureAwait(false);
            if (preparation.Error != null) return preparation.Error;

            List<TableDetail> selectedTables = SelectTables(preparation.Detail, request.TableIds);
            if (selectedTables.Count == 0)
            {
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, "No persisted table metadata was found for context generation.");
            }

            List<string> missingTableIds = BuildMissingTableIds(request.TableIds, selectedTables);
            if (missingTableIds.Count > 0)
            {
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, "The following table IDs were not found in persisted crawl metadata: " + String.Join(", ", missingTableIds) + ".");
            }

            return await GenerateTableContextsAsync(req, preparation, request, selectedTables).ConfigureAwait(false);
        }

        /// <summary>
        /// POST /v1/database/{id}/table-context/{tableId}/build - generate and persist context for one table.
        /// </summary>
        public async Task<object> BuildTableContextAsync(AppRequest req)
        {
            string id = req.Parameters["id"];
            string tableId = req.Parameters["tableId"];
            BuildTableContextRequest request = req.GetData<BuildTableContextRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            ChatPreparation preparation = await PrepareContextBuildAsync(req, id, request.ProviderId).ConfigureAwait(false);
            if (preparation.Error != null) return preparation.Error;

            TableDetail table = FindTable(preparation.Detail, tableId);
            if (table == null)
            {
                req.Http.Response.StatusCode = 404;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Table metadata '" + tableId + "' not found.");
            }

            List<TableDetail> selectedTables = new List<TableDetail> { table };
            return await GenerateTableContextsAsync(req, preparation, request, selectedTables).ConfigureAwait(false);
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
            ChatExecutionResult execution = await ExecuteChatResponseAsync(
                client,
                preparation,
                request,
                req.CancellationToken,
                null).ConfigureAwait(false);

            if (!execution.Success)
            {
                req.Http.Response.StatusCode = 502;
                return new ApiErrorResponse(ApiErrorEnum.InternalError, execution.Error ?? "Provider chat request failed.");
            }

            return new ChatResponseResult
            {
                Success = true,
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = execution.Model,
                Message = execution.Message,
                Telemetry = execution.Telemetry,
                ToolCalls = execution.ToolCalls,
                ExecutionPath = execution.ExecutionPath,
                CapabilityNotice = execution.CapabilityNotice
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
                Model = preparation.Provider.Model,
                ExecutionPath = DetermineInitialExecutionPath(preparation, request),
                CapabilityNotice = BuildCapabilityNotice(preparation, request)
            }, false).ConfigureAwait(false);

            using CompletionClientBase client = CreateClient(preparation.Provider);
            ChatExecutionResult execution = await ExecuteChatResponseAsync(
                client,
                preparation,
                request,
                req.CancellationToken,
                async (evt) => await SendChatEventAsync(req, evt, false).ConfigureAwait(false)).ConfigureAwait(false);

            if (!execution.Success)
            {
                await SendChatEventAsync(req, new ChatStreamEvent
                {
                    EventType = "error",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = preparation.Provider.Model,
                    Done = true,
                    Error = execution.Error ?? "Provider chat request failed.",
                    ExecutionPath = execution.ExecutionPath,
                    CapabilityNotice = execution.CapabilityNotice
                }, true).ConfigureAwait(false);
                return null;
            }

            if (!String.IsNullOrEmpty(execution.Message))
            {
                await SendChatEventAsync(req, new ChatStreamEvent
                {
                    EventType = "token",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = execution.Model,
                    Delta = execution.Message,
                    ExecutionPath = execution.ExecutionPath,
                    CapabilityNotice = execution.CapabilityNotice
                }, false).ConfigureAwait(false);
            }

            await SendChatEventAsync(req, new ChatStreamEvent
            {
                EventType = "completed",
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = execution.Model,
                Message = execution.Message,
                Telemetry = execution.Telemetry,
                ExecutionPath = execution.ExecutionPath,
                CapabilityNotice = execution.CapabilityNotice,
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

            DatabaseEntry database = await _Persistence.DatabaseConnections.ReadAsync(request.DatabaseId, req.CancellationToken).ConfigureAwait(false);
            if (database == null)
            {
                req.Http.Response.StatusCode = 404;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + request.DatabaseId + "' not found."));
            }

            string providerId = String.IsNullOrWhiteSpace(request.ProviderId) ? settings.Chat.DefaultProviderId : request.ProviderId;
            ModelProviderSettings provider = await _Persistence.ModelProviders.ReadAsync(providerId, req.CancellationToken).ConfigureAwait(false);
            if (provider == null || !provider.Enabled)
            {
                req.Http.Response.StatusCode = 404;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + providerId + "' not found or disabled."));
            }

            DatabaseDetail detail = _CrawlCache.Get(database.Id);
            if (detail == null)
                detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
            if (detail == null)
            {
                detail = await _CrawlCache.CrawlOneAsync(database).ConfigureAwait(false);
                await _Persistence.DatabaseMetadata.SaveCrawlAsync(detail, req.CancellationToken).ConfigureAwait(false);
            }

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

        private async Task<ChatPreparation> PrepareContextBuildAsync(AppRequest req, string databaseId, string providerId)
        {
            TablixSettings settings = _SettingsManager.Settings;
            if (!settings.Chat.Enabled)
            {
                req.Http.Response.StatusCode = 403;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.Forbidden, "Chat is disabled in server settings."));
            }

            DatabaseEntry database = await _Persistence.DatabaseConnections.ReadAsync(databaseId, req.CancellationToken).ConfigureAwait(false);
            if (database == null)
            {
                req.Http.Response.StatusCode = 404;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.NotFound, "Database '" + databaseId + "' not found."));
            }

            DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
            if (detail == null || !detail.IsCrawled)
                detail = _CrawlCache.Get(database.Id);
            if (detail == null || !detail.IsCrawled)
            {
                req.Http.Response.StatusCode = 409;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.Conflict, "A successful crawl is required before building context."));
            }

            string selectedProviderId = String.IsNullOrWhiteSpace(providerId) ? settings.Chat.DefaultProviderId : providerId;
            ModelProviderSettings provider = await _Persistence.ModelProviders.ReadAsync(selectedProviderId, req.CancellationToken).ConfigureAwait(false);
            if (provider == null || !provider.Enabled)
            {
                req.Http.Response.StatusCode = 404;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + selectedProviderId + "' not found or disabled."));
            }

            return new ChatPreparation
            {
                Database = database,
                Provider = provider,
                Detail = detail,
                Settings = settings
            };
        }

        private async Task<object> GenerateTableContextsAsync(AppRequest req, ChatPreparation preparation, BuildTableContextRequest request, List<TableDetail> selectedTables)
        {
            string instructions = String.IsNullOrWhiteSpace(request.Prompt) ? DefaultTableContextBuildInstructions() : request.Prompt.Trim();
            string systemPrompt = "You generate concise, durable table context for Tablix. Restrict output to the selected database, selected table, its structure, contents, and relationships. Do not include credentials, secrets, raw result rows, or speculative facts.";
            List<TableContextRead> contexts = new List<TableContextRead>();
            Stopwatch stopwatch = Stopwatch.StartNew();
            string model = preparation.Provider.Model;
            string[] promptTranscript = new string[selectedTables.Count];
            string[] responseTranscript = new string[selectedTables.Count];
            string[] responseModels = new string[selectedTables.Count];
            using SemaphoreSlim semaphore = new SemaphoreSlim(preparation.Provider.MaxConcurrentRequests, preparation.Provider.MaxConcurrentRequests);
            List<Task<TableContextRead>> tasks = new List<Task<TableContextRead>>();

            for (int index = 0; index < selectedTables.Count; index++)
            {
                int tableIndex = index;
                tasks.Add(GenerateOneTableContextAsync(
                    req,
                    preparation,
                    selectedTables[tableIndex],
                    instructions,
                    systemPrompt,
                    semaphore,
                    promptTranscript,
                    responseTranscript,
                    responseModels,
                    tableIndex));
            }

            TableContextRead[] savedContexts;
            try
            {
                savedContexts = await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (KeyNotFoundException ex)
            {
                stopwatch.Stop();
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                stopwatch.Stop();
                req.Http.Response.StatusCode = 502;
                return new ApiErrorResponse(ApiErrorEnum.InternalError, ex.Message);
            }

            foreach (TableContextRead saved in savedContexts)
            {
                if (saved == null) continue;
                contexts.Add(saved);
            }

            for (int index = 0; index < responseModels.Length; index++)
            {
                if (!String.IsNullOrWhiteSpace(responseModels[index]))
                {
                    model = responseModels[index];
                }
            }

            StringBuilder prompts = new StringBuilder();
            StringBuilder responses = new StringBuilder();
            AppendTranscripts(prompts, promptTranscript);
            AppendTranscripts(responses, responseTranscript);

            stopwatch.Stop();
            ChatTelemetry telemetry = CreateTelemetry(
                stopwatch.ElapsedMilliseconds,
                stopwatch.ElapsedMilliseconds,
                prompts.ToString(),
                responses.ToString(),
                null);

            return new BuildTableContextResponse
            {
                Success = true,
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = model,
                Objects = contexts,
                Telemetry = telemetry
            };
        }

        private async Task<TableContextRead> GenerateOneTableContextAsync(
            AppRequest req,
            ChatPreparation preparation,
            TableDetail table,
            string instructions,
            string systemPrompt,
            SemaphoreSlim semaphore,
            string[] promptTranscript,
            string[] responseTranscript,
            string[] responseModels,
            int tableIndex)
        {
            await semaphore.WaitAsync(req.CancellationToken).ConfigureAwait(false);
            try
            {
                string prompt = BuildTableContextPrompt(preparation.Database, preparation.Detail, table, instructions);
                promptTranscript[tableIndex] = prompt;

                using CompletionClientBase client = CreateClient(preparation.Provider);
                ChatCompletionOptions options = CreateOptions(preparation.Provider, systemPrompt);
                ChatResponse response = await client.ChatAsync(prompt, options, req.CancellationToken).ConfigureAwait(false);
                if (!response.Success)
                    throw new InvalidOperationException(response.Error ?? "Provider table context generation failed.");

                string context = NormalizeGeneratedContext(response.Text);
                if (String.IsNullOrWhiteSpace(context))
                    throw new InvalidOperationException("Provider returned empty table context for " + table.SchemaName + "." + table.TableName + ".");

                TableContextRead saved = await _Persistence.TableContexts.UpsertAsync(
                    preparation.Database.Id,
                    table.TableId,
                    context,
                    "replace",
                    "model",
                    req.CancellationToken).ConfigureAwait(false);

                table.Context = saved.Context;
                responseTranscript[tableIndex] = context;
                responseModels[tableIndex] = response.Model;
                return saved;
            }
            finally
            {
                semaphore.Release();
            }
        }

        private static void AppendTranscripts(StringBuilder builder, string[] transcripts)
        {
            foreach (string transcript in transcripts)
            {
                if (String.IsNullOrWhiteSpace(transcript)) continue;
                builder.AppendLine(transcript);
            }
        }

        private static List<TableDetail> SelectTables(DatabaseDetail detail, List<string> tableIds)
        {
            List<TableDetail> tables = new List<TableDetail>();
            if (detail == null || detail.Tables == null) return tables;

            if (tableIds == null || tableIds.Count == 0)
            {
                foreach (TableDetail table in detail.Tables)
                {
                    if (!String.IsNullOrWhiteSpace(table.TableId)) tables.Add(table);
                }

                return tables;
            }

            foreach (string tableId in tableIds)
            {
                TableDetail table = FindTable(detail, tableId);
                if (table != null) tables.Add(table);
            }

            return tables;
        }

        private static List<string> BuildMissingTableIds(List<string> tableIds, List<TableDetail> selectedTables)
        {
            List<string> missing = new List<string>();
            if (tableIds == null || tableIds.Count == 0) return missing;

            foreach (string tableId in tableIds)
            {
                if (String.IsNullOrWhiteSpace(tableId)) continue;

                bool found = selectedTables.Any(table => String.Equals(table.TableId, tableId, StringComparison.OrdinalIgnoreCase));
                if (!found) missing.Add(tableId);
            }

            return missing;
        }

        private static TableDetail FindTable(DatabaseDetail detail, string tableId)
        {
            if (detail == null || detail.Tables == null || String.IsNullOrWhiteSpace(tableId)) return null;
            return detail.Tables.FirstOrDefault(table => String.Equals(table.TableId, tableId, StringComparison.OrdinalIgnoreCase));
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

        private static string BuildPrompt(TablixSettings settings, DatabaseEntry database, DatabaseDetail detail, List<CoreChatMessage> messages)
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
                if (!String.IsNullOrWhiteSpace(table.Context))
                    builder.AppendLine("  Table context: " + table.Context);
                builder.AppendLine("  Columns: " + String.Join(", ", table.Columns.Select(column => column.ColumnName + " " + column.DataType + (column.IsPrimaryKey ? " primary key" : "") + (column.IsNullable ? " nullable" : " not null"))));
                if (table.ForeignKeys.Count > 0)
                    builder.AppendLine("  Declared FKs: " + String.Join("; ", table.ForeignKeys.Select(foreignKey => foreignKey.ColumnName + " -> " + foreignKey.ReferencedTable + "." + foreignKey.ReferencedColumn)));
            }

            if (detail.Tables.Count > tableLimit)
                builder.AppendLine("Additional tables were omitted from prompt context. Ask the user to narrow the task or inspect specific tables.");

            builder.AppendLine();
            builder.AppendLine("Conversation:");
            foreach (CoreChatMessage message in messages)
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

        private static string DefaultTableContextBuildInstructions()
        {
            return "Analyze each selected table from the crawled schema and produce concise durable table context. Include the table purpose, key columns, primary key, important foreign keys or inferred join paths, common filters, write-safety caveats, and how the table relates to the database. Clearly label inferred relationships and avoid secrets or raw data.";
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

        private static string BuildTableContextPrompt(DatabaseEntry database, DatabaseDetail detail, TableDetail table, string instructions)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Build persisted context for exactly one table in the selected Tablix database using only the last crawl below and the user's instructions.");
            builder.AppendLine("Output only the table context text to save. Do not wrap it in markdown fences. Do not include credentials, secrets, raw rows, or unrelated commentary.");
            builder.AppendLine();
            builder.AppendLine("User instructions:");
            builder.AppendLine(instructions);
            builder.AppendLine();
            builder.AppendLine("Database:");
            builder.AppendLine("- Id: " + database.Id);
            builder.AppendLine("- Name: " + (database.Name ?? database.DatabaseName ?? database.Filename ?? database.Id));
            builder.AppendLine("- Type: " + database.Type);
            builder.AppendLine("- Schema: " + (database.Schema ?? detail.Schema ?? "(default)"));
            builder.AppendLine();
            builder.AppendLine("Database context:");
            builder.AppendLine(String.IsNullOrWhiteSpace(database.Context) ? "(none)" : database.Context);
            builder.AppendLine();
            builder.AppendLine("Selected table:");
            builder.AppendLine("- Table id: " + table.TableId);
            builder.AppendLine("- Name: " + table.SchemaName + "." + table.TableName);
            builder.AppendLine("- Existing table context: " + (String.IsNullOrWhiteSpace(table.Context) ? "(none)" : table.Context));
            builder.AppendLine("- Columns: " + String.Join(", ", table.Columns.Select(column => column.ColumnName + " " + column.DataType + (column.IsPrimaryKey ? " primary key" : "") + (column.IsNullable ? " nullable" : " not null"))));
            if (table.ForeignKeys.Count > 0)
                builder.AppendLine("- Declared FKs: " + String.Join("; ", table.ForeignKeys.Select(foreignKey => foreignKey.ColumnName + " -> " + foreignKey.ReferencedTable + "." + foreignKey.ReferencedColumn)));
            if (table.Indexes.Count > 0)
                builder.AppendLine("- Indexes: " + String.Join("; ", table.Indexes.Select(index => index.IndexName + "(" + String.Join(", ", index.Columns) + ")" + (index.IsUnique ? " unique" : ""))));
            builder.AppendLine();
            builder.AppendLine("Other tables for relationship context:");

            List<TableDetail> relatedTables = detail.Tables
                .Where(candidate => !String.Equals(candidate.TableId, table.TableId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(candidate => candidate.SchemaName)
                .ThenBy(candidate => candidate.TableName)
                .Take(100)
                .ToList();

            foreach (TableDetail related in relatedTables)
            {
                builder.AppendLine("- " + related.SchemaName + "." + related.TableName + ": " + String.Join(", ", related.Columns.Select(column => column.ColumnName)));
            }

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

        private async Task<ChatExecutionResult> ExecuteChatResponseAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            ChatRequest request,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync)
        {
            ChatExecutionPolicy policy = BuildExecutionPolicy(preparation, request);
            if (!policy.ToolsEnabled)
            {
                return await ExecutePlainChatAsync(client, preparation, "execution_disabled", policy.CapabilityNotice, token).ConfigureAwait(false);
            }

            if (policy.UseNativeTools)
            {
                ChatExecutionResult nativeResult = await ExecuteNativeToolChatAsync(client, preparation, policy, token, sendEventAsync).ConfigureAwait(false);
                if (nativeResult.Success && nativeResult.ToolCalls.Count > 0)
                    return nativeResult;

                if (!nativeResult.Success && !policy.FallbackEnabled)
                    return nativeResult;

                if (!policy.UserAskedForData || policy.UserAskedOnlyForSql || !policy.FallbackEnabled)
                    return nativeResult.Success ? nativeResult : await ExecutePlainChatAsync(client, preparation, "native_failed_plain", nativeResult.CapabilityNotice, token).ConfigureAwait(false);

                ChatExecutionResult fallbackResult = await ExecuteFallbackPlanningAsync(client, preparation, policy, token, sendEventAsync).ConfigureAwait(false);
                fallbackResult.CapabilityNotice = "The model did not request a native tool for this data question. Tablix used server-side fallback execution.";
                return fallbackResult;
            }

            if (policy.UserAskedForData && !policy.UserAskedOnlyForSql && policy.FallbackEnabled)
                return await ExecuteFallbackPlanningAsync(client, preparation, policy, token, sendEventAsync).ConfigureAwait(false);

            return await ExecutePlainChatAsync(client, preparation, "plain", policy.CapabilityNotice, token).ConfigureAwait(false);
        }

        private async Task<ChatExecutionResult> ExecuteNativeToolChatAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            ChatExecutionPolicy policy,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<PromptChatMessage> messages = BuildPromptMessages(preparation);
            List<ChatToolCall> executedTools = new List<ChatToolCall>();
            ToolChatResponse response = null;

            for (int iteration = 0; iteration < policy.MaxNativeToolIterations; iteration++)
            {
                ToolChatRequest toolRequest = new ToolChatRequest
                {
                    Model = preparation.Provider.Model,
                    Messages = messages,
                    Tools = TablixChatToolDefinitions.Build(),
                    ToolChoice = "auto",
                    Temperature = preparation.Provider.Temperature,
                    TopP = preparation.Provider.TopP,
                    MaxTokens = preparation.Provider.MaxTokens
                };

                response = await client.ToolChatAsync(toolRequest, token).ConfigureAwait(false);
                if (!response.Success)
                {
                    stopwatch.Stop();
                    return new ChatExecutionResult
                    {
                        Success = false,
                        Error = response.Error,
                        Model = response.Model ?? preparation.Provider.Model,
                        ExecutionPath = "native_tool_call_failed",
                        CapabilityNotice = policy.CapabilityNotice,
                        ToolCalls = executedTools
                    };
                }

                if (response.ToolCalls == null || response.ToolCalls.Count == 0)
                    break;

                messages.Add(response.ToAssistantMessage());

                foreach (PromptToolCall toolCall in response.ToolCalls)
                {
                    ChatToolCall executed = await ExecutePromptToolCallAsync(preparation, toolCall, "native", token, sendEventAsync).ConfigureAwait(false);
                    executedTools.Add(executed);
                    string toolResultContent = BuildToolResultContent(executed);
                    messages.Add(PromptChatMessage.ToolResult(toolCall.Id, toolCall.Name, toolResultContent));
                }
            }

            if (executedTools.Count > 0)
            {
                ToolChatRequest finalRequest = new ToolChatRequest
                {
                    Model = preparation.Provider.Model,
                    Messages = messages,
                    Tools = new List<ToolDefinition>(),
                    ToolChoice = "none",
                    Temperature = preparation.Provider.Temperature,
                    TopP = preparation.Provider.TopP,
                    MaxTokens = preparation.Provider.MaxTokens
                };

                response = await client.ToolChatAsync(finalRequest, token).ConfigureAwait(false);
            }

            stopwatch.Stop();

            if (response == null)
            {
                return new ChatExecutionResult
                {
                    Success = false,
                    Error = "Provider returned no response.",
                    Model = preparation.Provider.Model,
                    ExecutionPath = "native_no_response",
                    CapabilityNotice = policy.CapabilityNotice,
                    ToolCalls = executedTools
                };
            }

            if (!response.Success)
            {
                return new ChatExecutionResult
                {
                    Success = false,
                    Error = response.Error,
                    Model = response.Model ?? preparation.Provider.Model,
                    ExecutionPath = "native_final_failed",
                    CapabilityNotice = policy.CapabilityNotice,
                    ToolCalls = executedTools
                };
            }

            string message = response.Text ?? String.Empty;
            return new ChatExecutionResult
            {
                Success = true,
                Message = message,
                Model = response.Model ?? preparation.Provider.Model,
                ExecutionPath = executedTools.Count > 0 ? "native_tool_calls" : "native_no_tool_call",
                CapabilityNotice = policy.CapabilityNotice,
                ToolCalls = executedTools,
                Telemetry = CreateTelemetry(response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds, response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds, preparation.Prompt, message, null)
            };
        }

        private async Task<ChatExecutionResult> ExecuteFallbackPlanningAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            ChatExecutionPolicy policy,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            ChatCompletionOptions plannerOptions = CreateOptions(preparation.Provider, BuildFallbackPlannerSystemPrompt(preparation));
            plannerOptions.Temperature = preparation.Settings.Chat.PromptProcessing.PlannerTemperature;
            string plannerPrompt = BuildFallbackPlannerPrompt(preparation);
            ChatResponse planResponse = null;
            FallbackQueryPlan plan = null;

            for (int attempt = 0; attempt < policy.MaxPlanningAttempts; attempt++)
            {
                planResponse = await client.ChatAsync(plannerPrompt, plannerOptions, token).ConfigureAwait(false);
                if (!planResponse.Success)
                    break;

                plan = ParseFallbackPlan(planResponse.Text);
                if (plan != null && plan.Execute && !String.IsNullOrWhiteSpace(plan.Query))
                    break;

                plannerPrompt = plannerPrompt + Environment.NewLine + "Return only valid JSON matching {\"Execute\":true,\"Query\":\"...\",\"Reason\":\"...\"}.";
            }

            if (planResponse == null || !planResponse.Success)
            {
                stopwatch.Stop();
                return new ChatExecutionResult
                {
                    Success = false,
                    Error = planResponse == null ? "Fallback planner returned no response." : planResponse.Error,
                    Model = preparation.Provider.Model,
                    ExecutionPath = "fallback_planner_failed",
                    CapabilityNotice = policy.CapabilityNotice
                };
            }

            if (plan == null || !plan.Execute || String.IsNullOrWhiteSpace(plan.Query))
            {
                stopwatch.Stop();
                return await ExecutePlainChatAsync(client, preparation, "fallback_no_plan", policy.CapabilityNotice, token).ConfigureAwait(false);
            }

            ChatToolCall toolCall = await ExecutePlannedQueryAsync(preparation, plan.Query, "fallback", token, sendEventAsync).ConfigureAwait(false);
            string followupPrompt = BuildToolFollowupPrompt(preparation.Prompt, planResponse.Text, toolCall);
            ChatCompletionOptions options = CreateOptions(preparation.Provider, preparation.SystemPrompt);
            ChatResponse finalResponse = await client.ChatAsync(followupPrompt, options, token).ConfigureAwait(false);
            stopwatch.Stop();

            string message = finalResponse.Success ? finalResponse.Text : "The query could not be executed: " + (toolCall.Error ?? finalResponse.Error);
            return new ChatExecutionResult
            {
                Success = true,
                Message = message,
                Model = finalResponse.Success ? finalResponse.Model : preparation.Provider.Model,
                ExecutionPath = "server_fallback",
                CapabilityNotice = policy.CapabilityNotice,
                ToolCalls = new List<ChatToolCall> { toolCall },
                Telemetry = CreateTelemetry(stopwatch.ElapsedMilliseconds, stopwatch.ElapsedMilliseconds, preparation.Prompt, message, null)
            };
        }

        private async Task<ChatExecutionResult> ExecutePlainChatAsync(CompletionClientBase client, ChatPreparation preparation, string executionPath, string capabilityNotice, CancellationToken token)
        {
            ChatCompletionOptions options = CreateOptions(preparation.Provider, preparation.SystemPrompt);
            Stopwatch stopwatch = Stopwatch.StartNew();
            ChatResponse response = await client.ChatAsync(preparation.Prompt, options, token).ConfigureAwait(false);
            stopwatch.Stop();

            if (!response.Success)
            {
                return new ChatExecutionResult
                {
                    Success = false,
                    Error = response.Error,
                    Model = response.Model ?? preparation.Provider.Model,
                    ExecutionPath = executionPath,
                    CapabilityNotice = capabilityNotice
                };
            }

            return new ChatExecutionResult
            {
                Success = true,
                Message = response.Text,
                Model = response.Model ?? preparation.Provider.Model,
                ExecutionPath = executionPath,
                CapabilityNotice = capabilityNotice,
                Telemetry = CreateTelemetry(response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds, response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds, preparation.Prompt, response.Text, null)
            };
        }

        private async Task<ChatToolCall> ExecutePromptToolCallAsync(ChatPreparation preparation, PromptToolCall promptToolCall, string phase, CancellationToken token, Func<ChatStreamEvent, Task> sendEventAsync)
        {
            ChatToolCall toolCall = new ChatToolCall
            {
                Id = String.IsNullOrWhiteSpace(promptToolCall.Id) ? Guid.NewGuid().ToString("n") : promptToolCall.Id,
                Name = promptToolCall.Name,
                Arguments = promptToolCall.ArgumentsJson,
                Phase = phase
            };

            if (!String.Equals(promptToolCall.Name, TablixChatToolDefinitions.ExecuteQueryToolName, StringComparison.Ordinal))
            {
                toolCall.Error = "Unknown tool '" + promptToolCall.Name + "'.";
                toolCall.Success = false;
                await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_completed").ConfigureAwait(false);
                return toolCall;
            }

            TablixExecuteQueryArguments arguments = null;
            try
            {
                arguments = Serializer.DeserializeJson<TablixExecuteQueryArguments>(promptToolCall.ArgumentsJson);
            }
            catch (Exception ex)
            {
                toolCall.Error = "Tool arguments could not be parsed: " + ex.Message;
                toolCall.Success = false;
                await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_completed").ConfigureAwait(false);
                return toolCall;
            }

            if (arguments == null)
                arguments = new TablixExecuteQueryArguments();

            if (!String.Equals(arguments.DatabaseId, preparation.Database.Id, StringComparison.OrdinalIgnoreCase))
            {
                toolCall.Error = "Tool call attempted to use database '" + arguments.DatabaseId + "' but chat is restricted to selected database '" + preparation.Database.Id + "'.";
                toolCall.Success = false;
                await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_completed").ConfigureAwait(false);
                return toolCall;
            }

            return await ExecutePlannedQueryAsync(preparation, arguments.Query, phase, token, sendEventAsync, toolCall).ConfigureAwait(false);
        }

        private async Task<ChatToolCall> ExecutePlannedQueryAsync(ChatPreparation preparation, string query, string phase, CancellationToken token, Func<ChatStreamEvent, Task> sendEventAsync, ChatToolCall existingToolCall = null)
        {
            ChatToolCall toolCall = existingToolCall ?? new ChatToolCall
            {
                Id = Guid.NewGuid().ToString("n"),
                Name = TablixChatToolDefinitions.ExecuteQueryToolName,
                Arguments = Serializer.SerializeJson(new TablixExecuteQueryArguments { DatabaseId = preparation.Database.Id, Query = query }, false),
                Phase = phase
            };

            await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_started").ConfigureAwait(false);

            ChatQueryExecutionResult executionResult = await _QueryExecution.ExecuteAsync(
                preparation.Database,
                query,
                preparation.Settings.Chat.PromptProcessing.RetryAfterSchemaRefresh,
                token).ConfigureAwait(false);

            toolCall.Success = executionResult.Success;
            toolCall.TotalMs = executionResult.TotalMs + executionResult.SchemaRefreshMs;
            toolCall.Error = executionResult.Error;
            toolCall.Result = TruncateToolResult(Serializer.SerializeJson(ChatQueryToolResult.From(executionResult), false), preparation.Settings.Chat.Tools.MaxToolOutputCharacters);

            await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_completed").ConfigureAwait(false);
            return toolCall;
        }

        private static async Task SendToolLifecycleEventAsync(Func<ChatStreamEvent, Task> sendEventAsync, ChatPreparation preparation, ChatToolCall toolCall, string eventType)
        {
            if (sendEventAsync == null) return;

            await sendEventAsync(new ChatStreamEvent
            {
                EventType = eventType,
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = preparation.Provider.Model,
                ToolCall = toolCall
            }).ConfigureAwait(false);
        }

        private static List<PromptChatMessage> BuildPromptMessages(ChatPreparation preparation)
        {
            return new List<PromptChatMessage>
            {
                PromptChatMessage.System(preparation.SystemPrompt),
                PromptChatMessage.User(preparation.Prompt)
            };
        }

        private static string BuildToolResultContent(ChatToolCall toolCall)
        {
            ToolResultEnvelope envelope = new ToolResultEnvelope
            {
                Success = toolCall.Success,
                Result = toolCall.Result,
                Error = toolCall.Error
            };

            return Serializer.SerializeJson(envelope, false);
        }

        private static ChatExecutionPolicy BuildExecutionPolicy(ChatPreparation preparation, ChatRequest request)
        {
            PromptProcessingSettings promptProcessing = preparation.Settings.Chat.PromptProcessing;
            bool toolsEnabled = preparation.Settings.Chat.Tools.Enabled && promptProcessing.Enabled;
            bool preferNativeTools = request.PreferNativeToolCalls ?? promptProcessing.PreferNativeToolCalls;
            bool fallbackEnabled = request.FallbackWhenNativeToolNotCalled ?? promptProcessing.FallbackWhenNativeToolNotCalled;
            bool useNativeTools = toolsEnabled && preferNativeTools && preparation.Provider.SupportsNativeToolCalls && preparation.Provider.UseNativeToolCalls;
            string userMessage = LastUserMessage(request);
            string normalizedUser = userMessage.ToLowerInvariant();

            return new ChatExecutionPolicy
            {
                ToolsEnabled = toolsEnabled,
                PreferNativeTools = preferNativeTools,
                FallbackEnabled = fallbackEnabled,
                UseNativeTools = useNativeTools,
                UserAskedForData = UserAskedForData(normalizedUser),
                UserAskedOnlyForSql = UserAskedOnlyForSql(normalizedUser),
                MaxNativeToolIterations = promptProcessing.MaxNativeToolIterations,
                MaxPlanningAttempts = promptProcessing.MaxPlanningAttempts,
                CapabilityNotice = BuildCapabilityNotice(preparation, request)
            };
        }

        private static string DetermineInitialExecutionPath(ChatPreparation preparation, ChatRequest request)
        {
            ChatExecutionPolicy policy = BuildExecutionPolicy(preparation, request);
            if (!policy.ToolsEnabled) return "execution_disabled";
            if (policy.UseNativeTools) return "native_tool_calls";
            if (policy.FallbackEnabled) return "server_fallback";
            return "plain";
        }

        private static string BuildCapabilityNotice(ChatPreparation preparation, ChatRequest request)
        {
            PromptProcessingSettings promptProcessing = preparation.Settings.Chat.PromptProcessing;
            if (!preparation.Settings.Chat.Tools.Enabled || !promptProcessing.Enabled)
                return "Database query execution is disabled for chat. The assistant can discuss schema and draft SQL, but it cannot run queries.";

            bool preferNativeTools = request.PreferNativeToolCalls ?? promptProcessing.PreferNativeToolCalls;
            bool fallbackEnabled = request.FallbackWhenNativeToolNotCalled ?? promptProcessing.FallbackWhenNativeToolNotCalled;

            if (preferNativeTools && preparation.Provider.SupportsNativeToolCalls && preparation.Provider.UseNativeToolCalls)
                return "Native tool calls are enabled for this provider. Tablix still validates every database query before execution.";

            if (fallbackEnabled)
                return "This provider is not configured for native tool calls. Tablix will use server-side query planning and execution for database data requests.";

            return "This provider is not configured for native tool calls and server-side fallback execution is disabled.";
        }

        private static string BuildFallbackPlannerSystemPrompt(ChatPreparation preparation)
        {
            return preparation.SystemPrompt + " You are now planning a Tablix tool call. Return only JSON matching the FallbackQueryPlan schema. Do not include markdown.";
        }

        private static string BuildFallbackPlannerPrompt(ChatPreparation preparation)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(preparation.Prompt);
            builder.AppendLine();
            builder.AppendLine("The latest user message asks for database data. Build one executable query if a permitted query can answer it.");
            builder.AppendLine("Return only compact JSON with properties Execute, Query, and Reason.");
            builder.AppendLine("Use the selected database ID only. Do not include semicolons.");
            builder.AppendLine("Example: {\"Execute\":true,\"Query\":\"SELECT COUNT(*) AS UserCount FROM users\",\"Reason\":\"The user asked for a user count.\"}");
            return builder.ToString();
        }

        private static FallbackQueryPlan ParseFallbackPlan(string text)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;

            string json = ExtractJsonPayload(text);
            if (String.IsNullOrWhiteSpace(json)) return null;

            try
            {
                return Serializer.DeserializeJson<FallbackQueryPlan>(json);
            }
            catch
            {
                return null;
            }
        }

        private static string ExtractJsonPayload(string text)
        {
            string normalized = text.Trim();
            Match fenced = Regex.Match(normalized, "```(?:json)?\\s*(?<json>[\\s\\S]*?)```", RegexOptions.IgnoreCase);
            if (fenced.Success)
                normalized = fenced.Groups["json"].Value.Trim();

            int start = normalized.IndexOf('{');
            int end = normalized.LastIndexOf('}');
            if (start < 0 || end <= start) return null;
            return normalized.Substring(start, end - start + 1);
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

        private static string LastUserMessage(ChatRequest request)
        {
            if (request == null || request.Messages == null) return String.Empty;

            for (int i = request.Messages.Count - 1; i >= 0; i--)
            {
                CoreChatMessage message = request.Messages[i];
                if (message != null && String.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                    return message.Content ?? String.Empty;
            }

            return String.Empty;
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

    }
}
