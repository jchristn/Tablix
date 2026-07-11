namespace Tablix.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Tablix.Core.Enums;

    /// <summary>
    /// Chat feature settings.
    /// </summary>
    public class ChatSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable chat features.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default provider identifier.
        /// </summary>
        public string DefaultProviderId { get; set; } = "provider_ollama_local";

        /// <summary>
        /// Whether chat responses should stream by default.
        /// </summary>
        public bool DefaultStreaming { get; set; } = true;

        /// <summary>
        /// Default system prompt used for database chat when a provider-specific prompt is not configured.
        /// </summary>
        public string SystemPrompt { get; set; } = "You are Tablix, a database assistant. Restrict your conversation to only the selected database, its structure, its contents, and their relationships. Use configured database context first, inspect schema before writing SQL, run only allowed query types, clearly label inferred relationships, and never reveal credentials or secret settings. If the user asks for data or an answer that requires database contents, and you have access to a Tablix query execution tool that can run an allowed query to answer it, execute the query instead of only describing SQL. Use the query tool with the selected database, one permitted SQL statement, no semicolons, and only the columns needed; then return the results in the form the user asked for. If query execution reports a bad or unknown column, missing column, or column type mismatch, refresh the database schema by crawling or discovering the relevant tables before retrying. When refreshed schema proves saved context was wrong or stale, update the database context with corrected column names, column types, and any corrected relationship guidance.";

        /// <summary>
        /// Maximum table summaries to include automatically in chat context. Values are clamped from 1 to 1000.
        /// </summary>
        public int MaxContextTables
        {
            get { return _MaxContextTables; }
            set { _MaxContextTables = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Settings for server-side database tools exposed to chat.
        /// </summary>
        public ChatToolSettings Tools
        {
            get { return _Tools; }
            set { if (value != null) _Tools = value; }
        }

        /// <summary>
        /// Configured model providers.
        /// </summary>
        public List<ModelProviderSettings> Providers
        {
            get { return _Providers; }
            set { _Providers = value ?? new List<ModelProviderSettings>(); }
        }

        #endregion

        #region Private-Members

        private int _MaxContextTables = 100;
        private ChatToolSettings _Tools = new ChatToolSettings();
        private List<ModelProviderSettings> _Providers = new List<ModelProviderSettings>
        {
            new ModelProviderSettings
            {
                Id = "provider_ollama_local",
                Name = "Local Ollama",
                Type = ModelProviderTypeEnum.Ollama,
                Endpoint = "http://ollama:11434",
                Model = "gemma3:4b",
                Enabled = true,
                DefaultStreaming = true,
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000
            },
            new ModelProviderSettings
            {
                Id = "provider_openai",
                Name = "OpenAI",
                Type = ModelProviderTypeEnum.OpenAI,
                Endpoint = "https://api.openai.com",
                Model = "gpt-4o-mini",
                Enabled = false,
                DefaultStreaming = true,
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000
            },
            new ModelProviderSettings
            {
                Id = "provider_openai_compatible",
                Name = "OpenAI Compatible",
                Type = ModelProviderTypeEnum.OpenAICompatible,
                Endpoint = "http://localhost:1234",
                Model = "local-model",
                Enabled = false,
                DefaultStreaming = true,
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000
            },
            new ModelProviderSettings
            {
                Id = "provider_gemini",
                Name = "Gemini",
                Type = ModelProviderTypeEnum.Gemini,
                Endpoint = "https://generativelanguage.googleapis.com",
                Model = "gemini-2.5-flash",
                Enabled = false,
                DefaultStreaming = true,
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000
            }
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatSettings()
        {
        }

        #endregion
    }
}
