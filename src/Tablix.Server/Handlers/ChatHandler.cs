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
        private const string MandatoryExecutionSystemPrompt = "Mandatory Tablix execution rules: when the user asks for database data, database contents, computed values, counts, examples from rows, or an answer that depends on actual rows, call the available Tablix query execution tool or use Tablix server-side execution if a permitted query can answer it. Do not merely return SQL for the user to run unless the user explicitly asks for SQL only or says not to execute. Never fabricate table contents, result rows, counts, IDs, names, dates, metrics, or other database facts. If execution is unavailable, denied, or fails, say the data could not be verified instead of inventing an answer.";

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
                DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
                if (detail == null)
                    detail = _CrawlCache.Get(database.Id);
                databaseSummaries.Add(DatabaseSummary.From(database, detail));
            }

            ChatOptionsResponse response = new ChatOptionsResponse
            {
                Enabled = settings.Chat.Enabled,
                DefaultProviderId = SelectEffectiveDefaultProviderId(settings.Chat.DefaultProviderId, providers),
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

            DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
            if (detail == null || !detail.IsCrawled)
                detail = _CrawlCache.Get(database.Id);
            if (detail == null || !detail.IsCrawled)
            {
                req.Http.Response.StatusCode = 409;
                return new ApiErrorResponse(ApiErrorEnum.Conflict, "A successful crawl is required before building context.");
            }

            string providerId = request.ProviderId;
            ModelProviderSettings provider = await ResolveProviderAsync(providerId, settings.Chat.DefaultProviderId, req.CancellationToken).ConfigureAwait(false);
            if (provider == null || !provider.Enabled)
            {
                req.Http.Response.StatusCode = 404;
                string errorProviderId = String.IsNullOrWhiteSpace(providerId) ? settings.Chat.DefaultProviderId : providerId;
                return new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + errorProviderId + "' not found or disabled.");
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
            FinalizeExecution(preparation, execution);

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
                VerifiedAnswer = execution.VerifiedAnswer,
                Ambiguities = execution.Ambiguities,
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
            ChatExecutionResult execution = await ExecuteChatResponseStreamingAsync(
                client,
                preparation,
                request,
                req.CancellationToken,
                async (evt) => await SendChatEventAsync(req, evt, false).ConfigureAwait(false)).ConfigureAwait(false);
            FinalizeExecution(preparation, execution);

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

            await SendChatEventAsync(req, new ChatStreamEvent
            {
                EventType = "completed",
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = execution.Model,
                Message = execution.Message,
                Telemetry = execution.Telemetry,
                VerifiedAnswer = execution.VerifiedAnswer,
                Ambiguities = execution.Ambiguities,
                ExecutionPath = execution.ExecutionPath,
                CapabilityNotice = execution.CapabilityNotice,
                Done = true
            }, true).ConfigureAwait(false);

            return null;
        }

        /// <summary>
        /// POST /v1/chat/prompt - preview the prepared chat prompt.
        /// </summary>
        public async Task<object> PromptPreviewAsync(AppRequest req)
        {
            ChatRequest request = req.GetData<ChatRequest>();
            ChatPreparation preparation = await PreparePromptPreviewAsync(req, request).ConfigureAwait(false);
            if (preparation.Error != null) return preparation.Error;

            string systemPrompt = preparation.SystemPrompt ?? String.Empty;
            string contextPrompt = preparation.Prompt ?? String.Empty;

            return new ChatPromptPreviewResponse
            {
                Success = true,
                DatabaseId = preparation.Database.Id,
                ProviderId = preparation.Provider.Id,
                Model = preparation.Provider.Model,
                SystemPrompt = systemPrompt,
                ContextPrompt = contextPrompt,
                SystemPromptCharacters = systemPrompt.Length,
                ContextPromptCharacters = contextPrompt.Length,
                SystemPromptEstimatedTokens = EstimateTokens(systemPrompt),
                ContextPromptEstimatedTokens = EstimateTokens(contextPrompt),
                ConversationMessages = request?.Messages?.Count ?? 0
            };
        }

        #endregion

        #region Private-Methods

        private async Task<ChatPreparation> PrepareChatAsync(AppRequest req, ChatRequest request)
        {
            return await PrepareChatAsync(req, request, true).ConfigureAwait(false);
        }

        private async Task<ChatPreparation> PreparePromptPreviewAsync(AppRequest req, ChatRequest request)
        {
            return await PrepareChatAsync(req, request, false).ConfigureAwait(false);
        }

        private async Task<ChatPreparation> PrepareChatAsync(AppRequest req, ChatRequest request, bool requireUserMessage)
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

            if (requireUserMessage && (request.Messages == null || request.Messages.Count == 0 || !request.Messages.Any(message => String.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))))
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

            string providerId = request.ProviderId;
            ModelProviderSettings provider = await ResolveProviderAsync(providerId, settings.Chat.DefaultProviderId, req.CancellationToken).ConfigureAwait(false);
            if (provider == null || !provider.Enabled)
            {
                req.Http.Response.StatusCode = 404;
                string errorProviderId = String.IsNullOrWhiteSpace(providerId) ? settings.Chat.DefaultProviderId : providerId;
                return ChatPreparation.Fail(new ApiErrorResponse(ApiErrorEnum.NotFound, "Provider '" + errorProviderId + "' not found or disabled."));
            }

            DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken).ConfigureAwait(false);
            if (detail == null)
                detail = _CrawlCache.Get(database.Id);
            if (detail == null)
            {
                detail = await _CrawlCache.CrawlOneAsync(database).ConfigureAwait(false);
                await _Persistence.DatabaseMetadata.SaveCrawlAsync(detail, req.CancellationToken).ConfigureAwait(false);
            }

            string systemPrompt = BuildEffectiveSystemPrompt(settings, provider);
            string latestUserMessage = request.Messages
                .Where(message => String.Equals(message.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(message => message.Content)
                .LastOrDefault();
            List<AmbiguitySignal> ambiguities = DatabaseIntelligenceBuilder.FindPromptAmbiguities(detail, database.Context, latestUserMessage);
            string prompt = BuildPrompt(settings, database, detail, request.Messages, ambiguities);

            return new ChatPreparation
            {
                Database = database,
                Provider = provider,
                Detail = detail,
                Settings = settings,
                SystemPrompt = systemPrompt,
                Prompt = prompt,
                LatestUserMessage = latestUserMessage,
                Ambiguities = ambiguities
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

            ModelProviderSettings provider = await ResolveProviderAsync(providerId, settings.Chat.DefaultProviderId, req.CancellationToken).ConfigureAwait(false);
            if (provider == null || !provider.Enabled)
            {
                req.Http.Response.StatusCode = 404;
                string selectedProviderId = String.IsNullOrWhiteSpace(providerId) ? settings.Chat.DefaultProviderId : providerId;
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

        private async Task<ModelProviderSettings> ResolveProviderAsync(string requestedProviderId, string configuredDefaultProviderId, CancellationToken token)
        {
            string selectedProviderId = String.IsNullOrWhiteSpace(requestedProviderId) ? configuredDefaultProviderId : requestedProviderId;
            if (!String.IsNullOrWhiteSpace(selectedProviderId))
            {
                ModelProviderSettings selectedProvider = await _Persistence.ModelProviders.ReadAsync(selectedProviderId, token).ConfigureAwait(false);
                if (selectedProvider != null && selectedProvider.Enabled)
                    return selectedProvider;

                bool selectedIsConfiguredDefault = String.Equals(selectedProviderId, configuredDefaultProviderId, StringComparison.OrdinalIgnoreCase);
                if (!selectedIsConfiguredDefault)
                    return null;
            }

            List<ModelProviderSettings> enabledProviders = await _Persistence.ModelProviders.EnumerateAsync(1000, 0, null, true, token).ConfigureAwait(false);
            if (enabledProviders == null || enabledProviders.Count == 0) return null;
            return enabledProviders[0];
        }

        private static string SelectEffectiveDefaultProviderId(string configuredDefaultProviderId, List<ModelProviderSettings> enabledProviders)
        {
            if (enabledProviders == null || enabledProviders.Count == 0) return configuredDefaultProviderId;

            foreach (ModelProviderSettings provider in enabledProviders)
            {
                if (provider == null) continue;
                if (String.Equals(provider.Id, configuredDefaultProviderId, StringComparison.OrdinalIgnoreCase))
                    return provider.Id;
            }

            ModelProviderSettings firstEnabledProvider = enabledProviders[0];
            return firstEnabledProvider == null ? configuredDefaultProviderId : firstEnabledProvider.Id;
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

        private static string BuildEffectiveSystemPrompt(TablixSettings settings, ModelProviderSettings provider)
        {
            string configuredPrompt = String.IsNullOrWhiteSpace(provider.SystemPrompt) ? settings.Chat.SystemPrompt : provider.SystemPrompt;
            if (String.IsNullOrWhiteSpace(configuredPrompt))
                return MandatoryExecutionSystemPrompt;

            return configuredPrompt.Trim() + Environment.NewLine + Environment.NewLine + MandatoryExecutionSystemPrompt;
        }

        private static string BuildPrompt(TablixSettings settings, DatabaseEntry database, DatabaseDetail detail, List<CoreChatMessage> messages, List<AmbiguitySignal> ambiguities)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("You are answering questions about a configured Tablix database.");
            builder.AppendLine("Use the database context and schema below as authoritative metadata. Do not invent tables or columns.");
            builder.AppendLine("Execution contract: if the user asks for data, row contents, counts, totals, latest/top records, examples from rows, or a database change and an allowed query can answer or perform it, call the available Tablix query execution tool instead of only describing SQL. Provide one compatible SQL statement with no semicolons and only needed columns so Tablix can execute it, then answer from the tool result.");
            builder.AppendLine("Do not tell the user they can run a query when they asked for the answer; Tablix will execute permitted SQL and provide results. Return SQL text only when the user explicitly asks for SQL only, asks what query to use, or execution is unavailable or denied.");
            builder.AppendLine("Never fabricate database contents or result values. If no successful tool result is available for a data question, say the data could not be verified.");
            builder.AppendLine("If the user names a table, use that exact table when it exists; do not silently substitute a different table. If the named table is not present, explain the closest match or ask for clarification.");
            builder.AppendLine("Schema/context questions such as what database or tables are visible can be answered from the metadata in this prompt. Questions about actual row values, row examples, counts, totals, most recent/top records, or purchases require query execution.");
            builder.AppendLine("For count questions, use COUNT(*) with a clear alias. For table-content questions, summarize purpose and columns from schema/context; if the user asks for example rows, execute a small SELECT with explicit columns and a LIMIT.");
            builder.AppendLine("When answering parent/child questions such as recent orders plus what was purchased, first limit the parent rows in a CTE or subquery, then join child/detail tables. If the user asks for one answer row per parent record, aggregate child rows after the parent limit instead of applying LIMIT to joined detail rows.");
            builder.AppendLine("For relative date questions on static sample data, anchor the period to the latest relevant date present in the database when using the real current date would return no useful rows, and state the anchor date used.");
            builder.AppendLine("When context update tools are available, call them to persist durable database-wide or table-specific insights, relationships, naming conventions, or corrections that will help future conversations. Use the table context tool for facts about one exact table and the database context tool for database-wide facts.");
            builder.AppendLine("Context updates must be concise and reusable. Use append for incremental observations. Use replace only when explicitly asked to rewrite saved context or when producing a curated full replacement.");
            builder.AppendLine("Do not persist secrets, credentials, raw result rows, sensitive personal data, one-off answer values, or unsupported guesses. Clearly label inferred relationships.");
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
                builder.AppendLine("- " + table.SchemaName + "." + table.TableName + " (TableId: " + table.TableId + ")");
                if (!String.IsNullOrWhiteSpace(table.Context))
                    builder.AppendLine("  Table context: " + table.Context);
                builder.AppendLine("  Columns: " + String.Join(", ", table.Columns.Select(column => column.ColumnName + " " + column.DataType + (column.IsPrimaryKey ? " primary key" : "") + (column.IsNullable ? " nullable" : " not null"))));
                if (table.ForeignKeys.Count > 0)
                    builder.AppendLine("  Declared FKs: " + String.Join("; ", table.ForeignKeys.Select(foreignKey => foreignKey.ColumnName + " -> " + foreignKey.ReferencedTable + "." + foreignKey.ReferencedColumn)));
            }

            if (detail.Tables.Count > tableLimit)
                builder.AppendLine("Additional tables were omitted from prompt context. Ask the user to narrow the task or inspect specific tables.");

            if (ambiguities != null && ambiguities.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Ambiguity signals:");
                foreach (AmbiguitySignal ambiguity in ambiguities)
                {
                    builder.AppendLine("- " + ambiguity.Term + ": " + ambiguity.Question + " Candidates: " + String.Join("; ", ambiguity.Candidates.Take(8)));
                }
                builder.AppendLine("If an ambiguity signal affects the user's request, ask the clarifying question before executing SQL.");
            }

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
            if (ShouldClarifyAmbiguity(preparation))
                return BuildAmbiguityClarificationResult(preparation, policy.CapabilityNotice);

            if (!policy.ToolsEnabled)
            {
                return await ExecutePlainChatAsync(client, preparation, "execution_disabled", policy.CapabilityNotice, token).ConfigureAwait(false);
            }

            if (policy.UseNativeTools)
            {
                ChatExecutionResult nativeResult = await ExecuteNativeToolChatAsync(client, preparation, policy, token, sendEventAsync).ConfigureAwait(false);
                if (nativeResult.Success && nativeResult.ToolCalls.Count > 0)
                    return nativeResult;

                if (!nativeResult.Success && !policy.UseFallbackPlanner)
                    return nativeResult;

                if (!policy.UseFallbackPlanner)
                    return nativeResult.Success ? nativeResult : await ExecutePlainChatAsync(client, preparation, "native_failed_plain", nativeResult.CapabilityNotice, token).ConfigureAwait(false);

                ChatExecutionResult fallbackResult = await ExecuteFallbackPlanningAsync(client, preparation, policy, token, sendEventAsync, nativeResult.Success ? nativeResult : null).ConfigureAwait(false);
                if (fallbackResult.ExecutionPath == "server_fallback")
                    fallbackResult.CapabilityNotice = "The model did not request a native tool. Tablix used model-based server planning and executed the approved query.";
                return fallbackResult;
            }

            if (policy.UseFallbackPlanner)
                return await ExecuteFallbackPlanningAsync(client, preparation, policy, token, sendEventAsync).ConfigureAwait(false);

            return await ExecutePlainChatAsync(client, preparation, "plain", policy.CapabilityNotice, token).ConfigureAwait(false);
        }

        private async Task<ChatExecutionResult> ExecuteChatResponseStreamingAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            ChatRequest request,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync)
        {
            ChatExecutionPolicy policy = BuildExecutionPolicy(preparation, request);
            if (ShouldClarifyAmbiguity(preparation))
                return BuildAmbiguityClarificationResult(preparation, policy.CapabilityNotice);

            if (!policy.ToolsEnabled)
            {
                return await ExecutePlainChatStreamingAsync(client, preparation, "execution_disabled", policy.CapabilityNotice, token, sendEventAsync).ConfigureAwait(false);
            }

            if (policy.UseNativeTools)
            {
                ChatExecutionResult nativeResult = await ExecuteNativeToolChatStreamingAsync(client, preparation, policy, token, sendEventAsync).ConfigureAwait(false);
                if (nativeResult.Success && nativeResult.ToolCalls.Count > 0)
                    return nativeResult;

                if (!nativeResult.Success && !policy.UseFallbackPlanner)
                    return nativeResult;

                if (!policy.UseFallbackPlanner)
                    return nativeResult.Success ? nativeResult : await ExecutePlainChatStreamingAsync(client, preparation, "native_failed_plain", nativeResult.CapabilityNotice, token, sendEventAsync).ConfigureAwait(false);

                ChatExecutionResult fallbackResult = await ExecuteFallbackPlanningStreamingAsync(client, preparation, policy, token, sendEventAsync, nativeResult.Success ? nativeResult : null).ConfigureAwait(false);
                if (fallbackResult.ExecutionPath == "server_fallback")
                    fallbackResult.CapabilityNotice = "The model did not request a native tool. Tablix used model-based server planning and executed the approved query.";
                return fallbackResult;
            }

            if (policy.UseFallbackPlanner)
                return await ExecuteFallbackPlanningStreamingAsync(client, preparation, policy, token, sendEventAsync).ConfigureAwait(false);

            return await ExecutePlainChatStreamingAsync(client, preparation, "plain", policy.CapabilityNotice, token, sendEventAsync).ConfigureAwait(false);
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
                    Tools = TablixChatToolDefinitions.Build(preparation.Settings.Chat.Tools.AllowContextUpdates),
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

        private async Task<ChatExecutionResult> ExecuteNativeToolChatStreamingAsync(
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
                    Tools = TablixChatToolDefinitions.Build(preparation.Settings.Chat.Tools.AllowContextUpdates),
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

            if (executedTools.Count == 0)
            {
                return new ChatExecutionResult
                {
                    Success = true,
                    Message = response.Text ?? String.Empty,
                    Model = String.IsNullOrWhiteSpace(response.Model) ? preparation.Provider.Model : response.Model,
                    ExecutionPath = "native_no_tool_call",
                    CapabilityNotice = policy.CapabilityNotice,
                    ToolCalls = executedTools,
                    Telemetry = CreateTelemetry(response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds, response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds, preparation.Prompt, response.Text, null)
                };
            }

            string followupPrompt = BuildToolFollowupPrompt(preparation.Prompt, executedTools);
            ChatExecutionResult finalResult = await ExecutePromptStreamingAsync(
                client,
                preparation,
                followupPrompt,
                "native_tool_calls",
                policy.CapabilityNotice,
                executedTools,
                token,
                sendEventAsync).ConfigureAwait(false);

            return finalResult;
        }

        private async Task<ChatExecutionResult> ExecuteFallbackPlanningAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            ChatExecutionPolicy policy,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync,
            ChatExecutionResult noExecutionResult = null)
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
                if (plan != null && (!plan.Execute || !String.IsNullOrWhiteSpace(plan.Query)))
                    break;

                plannerPrompt = plannerPrompt + Environment.NewLine + "Return only valid JSON matching {\"Intent\":\"DataAnswerRequest\",\"Execute\":true,\"Query\":\"...\",\"Reason\":\"...\"}. If Execute is false, Query must be null or empty. If Execute is true, Query must be non-empty.";
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
                if (noExecutionResult != null)
                    return noExecutionResult;

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

        private async Task<ChatExecutionResult> ExecuteFallbackPlanningStreamingAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            ChatExecutionPolicy policy,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync,
            ChatExecutionResult noExecutionResult = null)
        {
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
                if (plan != null && (!plan.Execute || !String.IsNullOrWhiteSpace(plan.Query)))
                    break;

                plannerPrompt = plannerPrompt + Environment.NewLine + "Return only valid JSON matching {\"Intent\":\"DataAnswerRequest\",\"Execute\":true,\"Query\":\"...\",\"Reason\":\"...\"}. If Execute is false, Query must be null or empty. If Execute is true, Query must be non-empty.";
            }

            if (planResponse == null || !planResponse.Success)
            {
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
                if (noExecutionResult != null)
                    return noExecutionResult;

                return await ExecutePlainChatStreamingAsync(client, preparation, "fallback_no_plan", policy.CapabilityNotice, token, sendEventAsync).ConfigureAwait(false);
            }

            ChatToolCall toolCall = await ExecutePlannedQueryAsync(preparation, plan.Query, "fallback", token, sendEventAsync).ConfigureAwait(false);
            string followupPrompt = BuildToolFollowupPrompt(preparation.Prompt, planResponse.Text, toolCall);
            return await ExecutePromptStreamingAsync(
                client,
                preparation,
                followupPrompt,
                "server_fallback",
                policy.CapabilityNotice,
                new List<ChatToolCall> { toolCall },
                token,
                sendEventAsync).ConfigureAwait(false);
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

        private async Task<ChatExecutionResult> ExecutePlainChatStreamingAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            string executionPath,
            string capabilityNotice,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync)
        {
            return await ExecutePromptStreamingAsync(
                client,
                preparation,
                preparation.Prompt,
                executionPath,
                capabilityNotice,
                new List<ChatToolCall>(),
                token,
                sendEventAsync).ConfigureAwait(false);
        }

        private async Task<ChatExecutionResult> ExecutePromptStreamingAsync(
            CompletionClientBase client,
            ChatPreparation preparation,
            string prompt,
            string executionPath,
            string capabilityNotice,
            List<ChatToolCall> toolCalls,
            CancellationToken token,
            Func<ChatStreamEvent, Task> sendEventAsync)
        {
            ChatCompletionOptions options = CreateOptions(preparation.Provider, preparation.SystemPrompt);
            Stopwatch stopwatch = Stopwatch.StartNew();
            ChatStreamingResponse response = await client.ChatStreamingAsync(prompt, options, token).ConfigureAwait(false);
            if (!response.Success)
            {
                stopwatch.Stop();
                return new ChatExecutionResult
                {
                    Success = false,
                    Error = response.Error,
                    Model = String.IsNullOrWhiteSpace(response.Model) ? preparation.Provider.Model : response.Model,
                    ExecutionPath = executionPath,
                    CapabilityNotice = capabilityNotice,
                    ToolCalls = toolCalls
                };
            }

            StringBuilder messageBuilder = new StringBuilder();
            ChatStreamingUsage usage = null;

            await foreach (ChatStreamingChunk chunk in response.Chunks.WithCancellation(token).ConfigureAwait(false))
            {
                if (chunk.Usage != null)
                    usage = chunk.Usage;

                if (String.IsNullOrEmpty(chunk.Text))
                    continue;

                messageBuilder.Append(chunk.Text);
                await sendEventAsync(new ChatStreamEvent
                {
                    EventType = "token",
                    DatabaseId = preparation.Database.Id,
                    ProviderId = preparation.Provider.Id,
                    Model = String.IsNullOrWhiteSpace(chunk.Model) ? preparation.Provider.Model : chunk.Model,
                    Delta = chunk.Text,
                    ExecutionPath = executionPath,
                    CapabilityNotice = capabilityNotice
                }).ConfigureAwait(false);
            }

            stopwatch.Stop();
            string message = messageBuilder.ToString();
            long totalMs = response.OverallRuntimeMs > 0 ? response.OverallRuntimeMs : stopwatch.ElapsedMilliseconds;

            return new ChatExecutionResult
            {
                Success = true,
                Message = message,
                Model = String.IsNullOrWhiteSpace(response.Model) ? preparation.Provider.Model : response.Model,
                ExecutionPath = executionPath,
                CapabilityNotice = capabilityNotice,
                ToolCalls = toolCalls,
                Telemetry = CreateTelemetry(response.TimeToFirstTokenMs, totalMs, prompt, message, usage ?? response.Usage)
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

            if (String.Equals(promptToolCall.Name, TablixChatToolDefinitions.ExecuteQueryToolName, StringComparison.Ordinal))
                return await ExecuteQueryToolCallAsync(preparation, promptToolCall, phase, token, sendEventAsync, toolCall).ConfigureAwait(false);

            if (String.Equals(promptToolCall.Name, TablixChatToolDefinitions.UpdateDatabaseContextToolName, StringComparison.Ordinal))
                return await ExecuteDatabaseContextUpdateToolCallAsync(preparation, promptToolCall, token, sendEventAsync, toolCall).ConfigureAwait(false);

            if (String.Equals(promptToolCall.Name, TablixChatToolDefinitions.UpdateTableContextToolName, StringComparison.Ordinal))
                return await ExecuteTableContextUpdateToolCallAsync(preparation, promptToolCall, token, sendEventAsync, toolCall).ConfigureAwait(false);

            return await CompleteFailedToolCallAsync(preparation, toolCall, "Unknown tool '" + promptToolCall.Name + "'.", sendEventAsync).ConfigureAwait(false);
        }

        private async Task<ChatToolCall> ExecuteQueryToolCallAsync(ChatPreparation preparation, PromptToolCall promptToolCall, string phase, CancellationToken token, Func<ChatStreamEvent, Task> sendEventAsync, ChatToolCall toolCall)
        {
            TablixExecuteQueryArguments arguments;
            string error;
            if (!TryDeserializeToolArguments(promptToolCall.ArgumentsJson, out arguments, out error))
                return await CompleteFailedToolCallAsync(preparation, toolCall, error, sendEventAsync).ConfigureAwait(false);

            if (!String.Equals(arguments.DatabaseId, preparation.Database.Id, StringComparison.OrdinalIgnoreCase))
            {
                return await CompleteFailedToolCallAsync(
                    preparation,
                    toolCall,
                    "Tool call attempted to use database '" + arguments.DatabaseId + "' but chat is restricted to selected database '" + preparation.Database.Id + "'.",
                    sendEventAsync).ConfigureAwait(false);
            }

            return await ExecutePlannedQueryAsync(preparation, arguments.Query, phase, token, sendEventAsync, toolCall).ConfigureAwait(false);
        }

        private async Task<ChatToolCall> ExecuteDatabaseContextUpdateToolCallAsync(ChatPreparation preparation, PromptToolCall promptToolCall, CancellationToken token, Func<ChatStreamEvent, Task> sendEventAsync, ChatToolCall toolCall)
        {
            TablixUpdateDatabaseContextArguments arguments;
            string error;
            if (!TryDeserializeToolArguments(promptToolCall.ArgumentsJson, out arguments, out error))
                return await CompleteFailedToolCallAsync(preparation, toolCall, error, sendEventAsync).ConfigureAwait(false);

            if (!preparation.Settings.Chat.Tools.AllowContextUpdates)
                return await CompleteFailedToolCallAsync(preparation, toolCall, "Context update tools are disabled for chat.", sendEventAsync).ConfigureAwait(false);

            if (!String.Equals(arguments.DatabaseId, preparation.Database.Id, StringComparison.OrdinalIgnoreCase))
            {
                return await CompleteFailedToolCallAsync(
                    preparation,
                    toolCall,
                    "Tool call attempted to use database '" + arguments.DatabaseId + "' but chat is restricted to selected database '" + preparation.Database.Id + "'.",
                    sendEventAsync).ConfigureAwait(false);
            }

            string mode = NormalizeContextUpdateMode(arguments.Mode, out error);
            if (!String.IsNullOrWhiteSpace(error))
                return await CompleteFailedToolCallAsync(preparation, toolCall, error, sendEventAsync).ConfigureAwait(false);

            if (String.IsNullOrWhiteSpace(arguments.Context))
                return await CompleteFailedToolCallAsync(preparation, toolCall, "Context is required for database context updates.", sendEventAsync).ConfigureAwait(false);

            await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_started").ConfigureAwait(false);

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                string context = await _Persistence.DatabaseContexts.UpsertAsync(
                    preparation.Database.Id,
                    arguments.Context,
                    mode,
                    "chat",
                    token).ConfigureAwait(false);

                stopwatch.Stop();
                preparation.Database.Context = context;
                preparation.Detail.Context = context;

                DatabaseDetail cached = _CrawlCache.Get(preparation.Database.Id);
                if (cached != null)
                    cached.Context = context;

                ChatContextUpdateToolResult result = new ChatContextUpdateToolResult
                {
                    Success = true,
                    Scope = ContextScopeEnum.Database,
                    DatabaseId = preparation.Database.Id,
                    Mode = mode,
                    Context = context,
                    Reason = arguments.Reason
                };

                toolCall.Success = true;
                toolCall.TotalMs = stopwatch.ElapsedMilliseconds;
                toolCall.Result = TruncateToolResult(Serializer.SerializeJson(result, false), preparation.Settings.Chat.Tools.MaxToolOutputCharacters);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                toolCall.Success = false;
                toolCall.TotalMs = stopwatch.ElapsedMilliseconds;
                toolCall.Error = ex.Message;
            }

            await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_completed").ConfigureAwait(false);
            return toolCall;
        }

        private async Task<ChatToolCall> ExecuteTableContextUpdateToolCallAsync(ChatPreparation preparation, PromptToolCall promptToolCall, CancellationToken token, Func<ChatStreamEvent, Task> sendEventAsync, ChatToolCall toolCall)
        {
            TablixUpdateTableContextArguments arguments;
            string error;
            if (!TryDeserializeToolArguments(promptToolCall.ArgumentsJson, out arguments, out error))
                return await CompleteFailedToolCallAsync(preparation, toolCall, error, sendEventAsync).ConfigureAwait(false);

            if (!preparation.Settings.Chat.Tools.AllowContextUpdates)
                return await CompleteFailedToolCallAsync(preparation, toolCall, "Context update tools are disabled for chat.", sendEventAsync).ConfigureAwait(false);

            if (!String.Equals(arguments.DatabaseId, preparation.Database.Id, StringComparison.OrdinalIgnoreCase))
            {
                return await CompleteFailedToolCallAsync(
                    preparation,
                    toolCall,
                    "Tool call attempted to use database '" + arguments.DatabaseId + "' but chat is restricted to selected database '" + preparation.Database.Id + "'.",
                    sendEventAsync).ConfigureAwait(false);
            }

            string mode = NormalizeContextUpdateMode(arguments.Mode, out error);
            if (!String.IsNullOrWhiteSpace(error))
                return await CompleteFailedToolCallAsync(preparation, toolCall, error, sendEventAsync).ConfigureAwait(false);

            if (String.IsNullOrWhiteSpace(arguments.Context))
                return await CompleteFailedToolCallAsync(preparation, toolCall, "Context is required for table context updates.", sendEventAsync).ConfigureAwait(false);

            TableDetail table = ResolveContextUpdateTable(preparation.Detail, arguments, out error);
            if (table == null)
                return await CompleteFailedToolCallAsync(preparation, toolCall, error, sendEventAsync).ConfigureAwait(false);

            await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_started").ConfigureAwait(false);

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                TableContextRead context = await _Persistence.TableContexts.UpsertAsync(
                    preparation.Database.Id,
                    table.TableId,
                    arguments.Context,
                    mode,
                    "chat",
                    token).ConfigureAwait(false);

                stopwatch.Stop();
                table.Context = context.Context;
                UpdateCachedTableContext(preparation.Database.Id, context.TableId, context.SchemaName, context.TableName, context.Context);

                ChatContextUpdateToolResult result = new ChatContextUpdateToolResult
                {
                    Success = true,
                    Scope = ContextScopeEnum.Table,
                    DatabaseId = preparation.Database.Id,
                    TableId = context.TableId,
                    SchemaName = context.SchemaName,
                    TableName = context.TableName,
                    Mode = mode,
                    Context = context.Context,
                    Reason = arguments.Reason
                };

                toolCall.Success = true;
                toolCall.TotalMs = stopwatch.ElapsedMilliseconds;
                toolCall.Result = TruncateToolResult(Serializer.SerializeJson(result, false), preparation.Settings.Chat.Tools.MaxToolOutputCharacters);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                toolCall.Success = false;
                toolCall.TotalMs = stopwatch.ElapsedMilliseconds;
                toolCall.Error = ex.Message;
            }

            await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_completed").ConfigureAwait(false);
            return toolCall;
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

        private static bool TryDeserializeToolArguments<T>(string argumentsJson, out T arguments, out string error) where T : new()
        {
            arguments = default(T);
            error = null;

            try
            {
                arguments = Serializer.DeserializeJson<T>(argumentsJson);
            }
            catch (Exception ex)
            {
                error = "Tool arguments could not be parsed: " + ex.Message;
                return false;
            }

            if (arguments == null)
                arguments = new T();

            return true;
        }

        private static async Task<ChatToolCall> CompleteFailedToolCallAsync(ChatPreparation preparation, ChatToolCall toolCall, string error, Func<ChatStreamEvent, Task> sendEventAsync)
        {
            toolCall.Error = error;
            toolCall.Success = false;
            await SendToolLifecycleEventAsync(sendEventAsync, preparation, toolCall, "tool_completed").ConfigureAwait(false);
            return toolCall;
        }

        private static string NormalizeContextUpdateMode(string requestedMode, out string error)
        {
            error = null;

            if (String.IsNullOrWhiteSpace(requestedMode))
                return "append";

            string mode = requestedMode.Trim().ToLowerInvariant();
            if (String.Equals(mode, "append", StringComparison.Ordinal) || String.Equals(mode, "replace", StringComparison.Ordinal))
                return mode;

            error = "Unsupported context update mode '" + requestedMode + "'. Use append or replace.";
            return null;
        }

        private static TableDetail ResolveContextUpdateTable(DatabaseDetail detail, TablixUpdateTableContextArguments arguments, out string error)
        {
            error = null;

            if (arguments == null)
            {
                error = "Tool arguments are required.";
                return null;
            }

            if (detail == null || detail.Tables == null)
            {
                error = "No table metadata is available for the selected database.";
                return null;
            }

            if (!String.IsNullOrWhiteSpace(arguments.TableId))
            {
                TableDetail tableById = FindTable(detail, arguments.TableId);
                if (tableById == null)
                    error = "TableId '" + arguments.TableId + "' was not found in selected database metadata.";
                return tableById;
            }

            if (String.IsNullOrWhiteSpace(arguments.TableName))
            {
                error = "Either TableId or TableName is required for table context updates.";
                return null;
            }

            List<TableDetail> matches = FindTablesByName(detail, arguments.SchemaName, arguments.TableName);
            if (matches.Count == 1)
                return matches[0];

            if (matches.Count > 1)
                error = "TableName '" + arguments.TableName + "' matched multiple tables; include TableId or SchemaName.";
            else
                error = "TableName '" + arguments.TableName + "' was not found in selected database metadata.";

            return null;
        }

        private static List<TableDetail> FindTablesByName(DatabaseDetail detail, string schemaName, string tableName)
        {
            List<TableDetail> matches = new List<TableDetail>();
            if (detail == null || detail.Tables == null || String.IsNullOrWhiteSpace(tableName))
                return matches;

            string schema = String.IsNullOrWhiteSpace(schemaName) ? null : schemaName.Trim();
            string name = tableName.Trim();

            int separator = name.IndexOf(".", StringComparison.Ordinal);
            if (String.IsNullOrWhiteSpace(schema) && separator > 0 && separator < name.Length - 1)
            {
                schema = name.Substring(0, separator).Trim();
                name = name.Substring(separator + 1).Trim();
            }

            foreach (TableDetail table in detail.Tables)
            {
                bool nameMatches = String.Equals(table.TableName, name, StringComparison.OrdinalIgnoreCase);
                bool schemaMatches = String.IsNullOrWhiteSpace(schema) || String.Equals(table.SchemaName, schema, StringComparison.OrdinalIgnoreCase);
                if (nameMatches && schemaMatches)
                    matches.Add(table);
            }

            return matches;
        }

        private void UpdateCachedTableContext(string databaseId, string tableId, string schemaName, string tableName, string context)
        {
            DatabaseDetail cached = _CrawlCache.Get(databaseId);
            if (cached == null) return;

            TableDetail cachedTable = FindTable(cached, tableId);
            if (cachedTable == null)
            {
                List<TableDetail> matches = FindTablesByName(cached, schemaName, tableName);
                if (matches.Count == 1)
                    cachedTable = matches[0];
            }

            if (cachedTable != null)
                cachedTable.Context = context;
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

        private static void FinalizeExecution(ChatPreparation preparation, ChatExecutionResult execution)
        {
            if (execution == null) return;

            if (execution.Ambiguities.Count == 0 && preparation.Ambiguities != null)
                execution.Ambiguities = preparation.Ambiguities;

            if (execution.VerifiedAnswer == null)
                execution.VerifiedAnswer = BuildVerifiedAnswer(preparation, execution);
        }

        private static bool ShouldClarifyAmbiguity(ChatPreparation preparation)
        {
            if (preparation == null || preparation.Ambiguities == null || preparation.Ambiguities.Count == 0) return false;
            if (!LooksLikeDataRequest(preparation.LatestUserMessage)) return false;

            return true;
        }

        private static ChatExecutionResult BuildAmbiguityClarificationResult(ChatPreparation preparation, string capabilityNotice)
        {
            string message = BuildAmbiguityClarificationMessage(preparation.Ambiguities);
            VerifiedAnswer verifiedAnswer = new VerifiedAnswer
            {
                State = "ambiguous",
                Summary = "Tablix did not execute SQL because the request has multiple plausible database interpretations.",
                Evidence = preparation.Ambiguities.Select(ambiguity => ambiguity.Question).ToList()
            };

            return new ChatExecutionResult
            {
                Success = true,
                Message = message,
                Model = preparation.Provider.Model,
                ExecutionPath = "ambiguity_check",
                CapabilityNotice = capabilityNotice,
                Ambiguities = preparation.Ambiguities,
                VerifiedAnswer = verifiedAnswer,
                Telemetry = CreateTelemetry(0, 0, preparation.Prompt, message, null)
            };
        }

        private static string BuildAmbiguityClarificationMessage(List<AmbiguitySignal> ambiguities)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("I need one clarification before I run SQL.");
            builder.AppendLine();

            foreach (AmbiguitySignal ambiguity in ambiguities.Take(3))
            {
                builder.AppendLine("- " + ambiguity.Question);
                if (ambiguity.Candidates.Count > 0)
                    builder.AppendLine("  Candidates: " + String.Join("; ", ambiguity.Candidates.Take(6)));
            }

            return builder.ToString().Trim();
        }

        private static VerifiedAnswer BuildVerifiedAnswer(ChatPreparation preparation, ChatExecutionResult execution)
        {
            if (execution == null)
            {
                return new VerifiedAnswer
                {
                    State = "blocked",
                    Summary = "No execution result was available."
                };
            }

            ChatToolCall successfulQuery = execution.ToolCalls.LastOrDefault(toolCall =>
                String.Equals(toolCall.Name, TablixChatToolDefinitions.ExecuteQueryToolName, StringComparison.Ordinal) &&
                toolCall.Success);

            if (successfulQuery != null)
                return BuildVerifiedAnswerFromSuccessfulQuery(successfulQuery);

            ChatToolCall failedQuery = execution.ToolCalls.LastOrDefault(toolCall =>
                String.Equals(toolCall.Name, TablixChatToolDefinitions.ExecuteQueryToolName, StringComparison.Ordinal));

            if (failedQuery != null)
                return BuildVerifiedAnswerFromFailedQuery(failedQuery);

            if (preparation.Ambiguities != null && preparation.Ambiguities.Count > 0 && String.Equals(execution.ExecutionPath, "ambiguity_check", StringComparison.OrdinalIgnoreCase))
            {
                return new VerifiedAnswer
                {
                    State = "ambiguous",
                    Summary = "No SQL was executed because Tablix needs a clarified database definition.",
                    Evidence = preparation.Ambiguities.Select(ambiguity => ambiguity.Question).ToList()
                };
            }

            if (LooksLikeDataRequest(preparation.LatestUserMessage))
            {
                return new VerifiedAnswer
                {
                    State = "blocked",
                    Summary = "No database query was executed, so row-dependent data was not verified.",
                    Error = execution.CapabilityNotice
                };
            }

            return new VerifiedAnswer
            {
                State = "partial",
                Summary = "No SQL execution was required; the answer is based on schema and saved context.",
                Evidence = new List<string> { "Selected database: " + preparation.Database.Id, "Execution path: " + (execution.ExecutionPath ?? "plain") }
            };
        }

        private static VerifiedAnswer BuildVerifiedAnswerFromSuccessfulQuery(ChatToolCall toolCall)
        {
            TablixExecuteQueryArguments arguments = TryReadQueryArguments(toolCall.Arguments);
            ChatQueryToolResult toolResult = TryReadQueryToolResult(toolCall.Result);
            QueryResult queryResult = toolResult == null ? null : toolResult.QueryResult;
            List<string> evidence = new List<string>();
            evidence.Add("Tablix executed one permitted SQL statement against the selected database.");
            if (queryResult != null)
            {
                evidence.Add("Rows returned: " + queryResult.RowsReturned + ".");
                evidence.Add("Runtime: " + queryResult.TotalMs.ToString("0.0") + " ms.");
                if (queryResult.Data != null && queryResult.Data.Columns != null && queryResult.Data.Columns.Count > 0)
                    evidence.Add("Columns: " + String.Join(", ", queryResult.Data.Columns.Select(column => column.Name).Take(12)) + ".");
            }

            return new VerifiedAnswer
            {
                State = "verified",
                Summary = "Verified by SQL execution through Tablix.",
                Sql = arguments == null ? null : arguments.Query,
                ToolCallId = toolCall.Id,
                RowsReturned = queryResult == null ? null : (int?)queryResult.RowsReturned,
                Evidence = evidence
            };
        }

        private static VerifiedAnswer BuildVerifiedAnswerFromFailedQuery(ChatToolCall toolCall)
        {
            TablixExecuteQueryArguments arguments = TryReadQueryArguments(toolCall.Arguments);
            return new VerifiedAnswer
            {
                State = "blocked",
                Summary = "Tablix attempted to verify the answer with SQL, but execution failed.",
                Sql = arguments == null ? null : arguments.Query,
                ToolCallId = toolCall.Id,
                Error = toolCall.Error,
                Evidence = new List<string> { "Failed tool phase: " + (toolCall.Phase ?? "unknown") }
            };
        }

        private static TablixExecuteQueryArguments TryReadQueryArguments(string json)
        {
            try
            {
                return Serializer.DeserializeJson<TablixExecuteQueryArguments>(json);
            }
            catch
            {
                return null;
            }
        }

        private static ChatQueryToolResult TryReadQueryToolResult(string json)
        {
            try
            {
                return Serializer.DeserializeJson<ChatQueryToolResult>(json);
            }
            catch
            {
                return null;
            }
        }

        private static bool LooksLikeDataRequest(string message)
        {
            if (String.IsNullOrWhiteSpace(message)) return false;
            return Regex.IsMatch(
                message,
                @"\b(show|list|count|total|average|latest|top|find|revenue|active|status)\b|\bhow\s+many\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static ChatExecutionPolicy BuildExecutionPolicy(ChatPreparation preparation, ChatRequest request)
        {
            PromptProcessingSettings promptProcessing = preparation.Settings.Chat.PromptProcessing;
            bool toolsEnabled = preparation.Settings.Chat.Tools.Enabled && promptProcessing.Enabled;
            bool preferNativeTools = request.PreferNativeToolCalls ?? promptProcessing.PreferNativeToolCalls;
            bool fallbackEnabled = request.FallbackWhenNativeToolNotCalled ?? promptProcessing.FallbackWhenNativeToolNotCalled;
            bool useNativeTools = toolsEnabled && preferNativeTools && preparation.Provider.SupportsNativeToolCalls && preparation.Provider.UseNativeToolCalls;

            return new ChatExecutionPolicy
            {
                ToolsEnabled = toolsEnabled,
                PreferNativeTools = preferNativeTools,
                FallbackEnabled = fallbackEnabled,
                UseNativeTools = useNativeTools,
                UseFallbackPlanner = toolsEnabled && fallbackEnabled && promptProcessing.RequireExecutionForDataRequests,
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
                return "This provider is not configured for native tool calls. Tablix will use model-based server planning to decide whether a database query should run.";

            return "This provider is not configured for native tool calls and server-side fallback execution is disabled.";
        }

        private static string BuildFallbackPlannerSystemPrompt(ChatPreparation preparation)
        {
            return preparation.SystemPrompt + " You are now deciding whether Tablix should execute a database query. Return only JSON matching the FallbackQueryPlan schema. Do not include markdown.";
        }

        private static string BuildFallbackPlannerPrompt(ChatPreparation preparation)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(preparation.Prompt);
            builder.AppendLine();
            builder.AppendLine("Classify the latest user message and decide whether Tablix should execute one database query.");
            builder.AppendLine("Return only compact JSON with properties Intent, Execute, Query, and Reason.");
            builder.AppendLine("Intent must be one of: Unknown, DataAnswerRequest, SqlOnlyRequest, SchemaQuestion, ContextQuestion, DatabaseConversation, ExplicitWriteRequest.");
            builder.AppendLine("Prompt-processing settings:");
            builder.AppendLine("- Preserve explicit SQL-only requests: " + preparation.Settings.Chat.PromptProcessing.AllowSqlOnlyByExplicitRequest);
            builder.AppendLine("Set Execute to true only when the user wants actual database row data, computed values from rows, or an explicit database action and one permitted query can satisfy the request.");
            builder.AppendLine("Set Execute to false when the user asks how to query, asks about schema/context without row data, is making conversation, explicitly says not to run a query, or the request is ambiguous.");
            builder.AppendLine("When Preserve explicit SQL-only requests is true, SQL-only requests must use Intent SqlOnlyRequest and Execute false.");
            builder.AppendLine("When Execute is false, Query must be null or empty.");
            builder.AppendLine("Use the selected database ID only. Do not include semicolons.");
            builder.AppendLine("For counts, prefer SELECT COUNT(*) with a clear alias.");
            builder.AppendLine("For parent/detail requests such as recent orders and purchased items, limit parent rows in a CTE or subquery before joining detail rows. Aggregate detail rows when the user expects one row per parent.");
            builder.AppendLine("For relative date windows on static sample data, use the latest relevant date in the database as the anchor if filtering against the real current date would return no useful rows.");
            builder.AppendLine("Examples:");
            builder.AppendLine("{\"Intent\":\"DataAnswerRequest\",\"Execute\":true,\"Query\":\"SELECT COUNT(*) AS UserCount FROM users\",\"Reason\":\"The user asked for a user count.\"}");
            builder.AppendLine("{\"Intent\":\"DataAnswerRequest\",\"Execute\":true,\"Query\":\"WITH last_orders AS (SELECT Id, UserId, OrderDate FROM orders ORDER BY OrderDate DESC, Id DESC LIMIT 3) SELECT o.Id AS OrderId, o.OrderDate, u.Name AS UserName, group_concat(li.ProductName || ' x' || li.Quantity, ', ') AS Items FROM last_orders o JOIN users u ON u.Id = o.UserId LEFT JOIN line_items li ON li.OrderId = o.Id GROUP BY o.Id, o.OrderDate, u.Name ORDER BY o.OrderDate DESC, o.Id DESC\",\"Reason\":\"The user asked who placed the most recent three orders and what they bought.\"}");
            builder.AppendLine("{\"Intent\":\"SqlOnlyRequest\",\"Execute\":false,\"Query\":null,\"Reason\":\"The user asked for SQL text instead of execution.\"}");
            builder.AppendLine("{\"Intent\":\"SchemaQuestion\",\"Execute\":false,\"Query\":null,\"Reason\":\"The user asked about table structure, not row data.\"}");
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

        private static string BuildToolFollowupPrompt(string originalPrompt, List<ChatToolCall> toolCalls)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine(originalPrompt);
            builder.AppendLine();
            builder.AppendLine("Tablix executed the following tool calls requested by the model:");

            foreach (ChatToolCall toolCall in toolCalls)
            {
                builder.AppendLine();
                builder.AppendLine("Tool: " + toolCall.Name);
                builder.AppendLine("Phase: " + toolCall.Phase);
                builder.AppendLine("Arguments: " + toolCall.Arguments);
                builder.AppendLine("Success: " + toolCall.Success);
                if (!String.IsNullOrWhiteSpace(toolCall.Error))
                    builder.AppendLine("Error: " + toolCall.Error);
                if (!String.IsNullOrWhiteSpace(toolCall.Result))
                    builder.AppendLine("Result: " + toolCall.Result);
            }

            builder.AppendLine();
            builder.AppendLine("Now answer the user's latest request using only these tool results and the selected database context. Do not say the user can run a query. If values were returned, state them directly in the format the user requested.");
            return builder.ToString();
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
