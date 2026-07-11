namespace Tablix.Server.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SwiftStack.Rest;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence;
    using Tablix.Core.Settings;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;

    /// <summary>
    /// REST handlers for server settings.
    /// </summary>
    public class SettingsHandler
    {
        #region Private-Members

        private readonly SettingsManager _SettingsManager;
        private readonly DatabaseDriverBase _Persistence;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="persistence">Persistence driver.</param>
        public SettingsHandler(SettingsManager settingsManager, DatabaseDriverBase persistence)
        {
            _SettingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// GET /v1/settings - read redacted server settings.
        /// </summary>
        public Task<object> GetSettingsAsync(AppRequest req)
        {
            return Task.FromResult((object)CreateReadResponse(_SettingsManager.Settings, _SettingsManager.Filename, _Persistence));
        }

        /// <summary>
        /// PUT /v1/settings - update editable server settings.
        /// </summary>
        public Task<object> UpdateSettingsAsync(AppRequest req)
        {
            SettingsUpdateRequest request = req.GetData<SettingsUpdateRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return Task.FromResult((object)new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required."));
            }

            List<string> apiKeys = request.ApiKeys
                .Where(apiKey => !String.IsNullOrWhiteSpace(apiKey))
                .Select(apiKey => apiKey.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (apiKeys.Count == 0)
            {
                req.Http.Response.StatusCode = 400;
                return Task.FromResult((object)new ApiErrorResponse(ApiErrorEnum.BadRequest, "At least one API key is required."));
            }

            TablixSettings existing = _SettingsManager.Settings;
            TablixSettings updated = new TablixSettings
            {
                Rest = request.Rest ?? existing.Rest,
                Logging = request.Logging ?? existing.Logging,
                Persistence = request.Persistence ?? existing.Persistence,
                ApiKeys = apiKeys,
                Chat = BuildChatSettings(existing.Chat, request.Chat)
            };

            _SettingsManager.UpdateSettings(updated);
            return Task.FromResult((object)CreateReadResponse(updated, _SettingsManager.Filename, _Persistence));
        }

        #endregion

        #region Private-Methods

        private static SettingsReadResponse CreateReadResponse(TablixSettings settings, string settingsFilename, DatabaseDriverBase persistence)
        {
            string resolvedFilename = PersistenceBootstrapper.ResolvePersistenceFilename(settingsFilename, settings.Persistence.Filename);
            SettingsReadResponse response = new SettingsReadResponse
            {
                Rest = settings.Rest,
                Logging = settings.Logging,
                Persistence = settings.Persistence,
                PersistenceHealth = new PersistenceHealthRead
                {
                    Type = settings.Persistence.Type,
                    Filename = settings.Persistence.Filename,
                    ResolvedFilename = resolvedFilename,
                    Healthy = persistence != null && File.Exists(resolvedFilename),
                    Message = persistence == null ? "Persistence has not initialized." : "Persistence initialized."
                },
                ApiKeys = new List<string>(settings.ApiKeys),
                Chat = CreateChatRead(settings.Chat),
                RestartRequiredPaths = new List<string>
                {
                    "Rest.Hostname",
                    "Rest.Port",
                    "Rest.Ssl",
                    "Rest.McpPort",
                    "Persistence.Type",
                    "Persistence.Filename",
                    "Logging.ConsoleLogging",
                    "Logging.FileLogging",
                    "Logging.LogDirectory",
                    "Logging.LogFilename",
                    "Logging.MinimumSeverity",
                    "Logging.EnableColors",
                    "Logging.Servers"
                }
            };

            return response;
        }

        private static ChatSettingsRead CreateChatRead(ChatSettings settings)
        {
            ChatSettingsRead read = new ChatSettingsRead
            {
                Enabled = settings.Enabled,
                DefaultProviderId = settings.DefaultProviderId,
                DefaultStreaming = settings.DefaultStreaming,
                SystemPrompt = settings.SystemPrompt,
                MaxContextTables = settings.MaxContextTables,
                Tools = settings.Tools,
                PromptProcessing = settings.PromptProcessing,
                Providers = new List<ModelProviderRead>()
            };

            return read;
        }

        private static ChatSettings BuildChatSettings(ChatSettings existing, ChatSettingsUpdate update)
        {
            if (update == null) return existing;

            ChatSettings settings = new ChatSettings
            {
                Enabled = update.Enabled,
                DefaultProviderId = update.DefaultProviderId,
                DefaultStreaming = update.DefaultStreaming,
                SystemPrompt = update.SystemPrompt,
                MaxContextTables = update.MaxContextTables,
                Tools = update.Tools ?? existing.Tools,
                PromptProcessing = update.PromptProcessing ?? existing.PromptProcessing
            };

            return settings;
        }

        #endregion
    }
}
