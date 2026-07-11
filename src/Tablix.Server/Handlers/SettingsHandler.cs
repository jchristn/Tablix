namespace Tablix.Server.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using SwiftStack.Rest;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;

    /// <summary>
    /// REST handlers for server settings.
    /// </summary>
    public class SettingsHandler
    {
        #region Private-Members

        private readonly SettingsManager _SettingsManager;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        public SettingsHandler(SettingsManager settingsManager)
        {
            _SettingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// GET /v1/settings - read redacted server settings.
        /// </summary>
        public Task<object> GetSettingsAsync(AppRequest req)
        {
            return Task.FromResult((object)CreateReadResponse(_SettingsManager.Settings));
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
                ApiKeys = apiKeys,
                Databases = existing.Databases,
                Chat = BuildChatSettings(existing.Chat, request.Chat)
            };

            _SettingsManager.UpdateSettings(updated);
            return Task.FromResult((object)CreateReadResponse(updated));
        }

        #endregion

        #region Private-Methods

        private static SettingsReadResponse CreateReadResponse(TablixSettings settings)
        {
            SettingsReadResponse response = new SettingsReadResponse
            {
                Rest = settings.Rest,
                Logging = settings.Logging,
                ApiKeys = new List<string>(settings.ApiKeys),
                Chat = CreateChatRead(settings.Chat),
                RestartRequiredPaths = new List<string>
                {
                    "Rest.Hostname",
                    "Rest.Port",
                    "Rest.Ssl",
                    "Rest.McpPort",
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
                Providers = settings.Providers.Select(CreateProviderRead).ToList()
            };

            return read;
        }

        private static ModelProviderRead CreateProviderRead(ModelProviderSettings provider)
        {
            return new ModelProviderRead
            {
                Id = provider.Id,
                Name = provider.Name,
                Type = provider.Type,
                Endpoint = provider.Endpoint,
                ApiKey = null,
                HasApiKey = !String.IsNullOrEmpty(provider.ApiKey),
                Model = provider.Model,
                SystemPrompt = provider.SystemPrompt,
                Enabled = provider.Enabled,
                DefaultStreaming = provider.DefaultStreaming,
                Temperature = provider.Temperature,
                TopP = provider.TopP,
                MaxTokens = provider.MaxTokens,
                RequestTimeoutMs = provider.RequestTimeoutMs
            };
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
                Providers = BuildProviders(existing.Providers, update.Providers)
            };

            if (String.IsNullOrWhiteSpace(settings.DefaultProviderId) && settings.Providers.Count > 0)
                settings.DefaultProviderId = settings.Providers[0].Id;

            return settings;
        }

        private static List<ModelProviderSettings> BuildProviders(List<ModelProviderSettings> existing, List<ModelProviderUpdate> updates)
        {
            List<ModelProviderSettings> providers = new List<ModelProviderSettings>();
            if (updates == null) return providers;

            foreach (ModelProviderUpdate update in updates)
            {
                if (update == null) continue;

                ModelProviderSettings previous = existing.FirstOrDefault(provider => String.Equals(provider.Id, update.Id, StringComparison.OrdinalIgnoreCase));
                string apiKey = null;
                if (update.ClearApiKey)
                    apiKey = null;
                else if (!String.IsNullOrEmpty(update.ApiKey))
                    apiKey = update.ApiKey;
                else if (previous != null)
                    apiKey = previous.ApiKey;

                providers.Add(new ModelProviderSettings
                {
                    Id = update.Id,
                    Name = update.Name,
                    Type = update.Type,
                    Endpoint = update.Endpoint,
                    ApiKey = apiKey,
                    Model = update.Model,
                    SystemPrompt = update.SystemPrompt,
                    Enabled = update.Enabled,
                    DefaultStreaming = update.DefaultStreaming,
                    Temperature = update.Temperature,
                    TopP = update.TopP,
                    MaxTokens = update.MaxTokens,
                    RequestTimeoutMs = update.RequestTimeoutMs
                });
            }

            return providers;
        }

        #endregion
    }
}
