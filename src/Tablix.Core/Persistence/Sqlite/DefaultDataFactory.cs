namespace Tablix.Core.Persistence.Sqlite
{
    using System.Collections.Generic;
    using Tablix.Core.Enums;
    using Tablix.Core.Settings;

    /// <summary>
    /// Default persisted product records for first boot.
    /// </summary>
    public static class DefaultDataFactory
    {
        /// <summary>
        /// Create the default local Ollama provider.
        /// </summary>
        /// <returns>Provider settings.</returns>
        public static ModelProviderSettings CreateOllamaProvider()
        {
            return new ModelProviderSettings
            {
                Id = "provider_ollama_local",
                Name = "Local Ollama",
                Type = ModelProviderTypeEnum.Ollama,
                Endpoint = "http://ollama:11434",
                Model = "gemma3:4b",
                Enabled = true,
                DefaultStreaming = true,
                SupportsNativeToolCalls = true,
                UseNativeToolCalls = false,
                SupportsStrictJson = false,
                ToolCapabilityNote = "Ollama supports tool-capable APIs for some models. Enable native tools when the selected local model reliably emits tool calls.",
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000,
                MaxConcurrentRequests = 1
            };
        }

        /// <summary>
        /// Create the default OpenAI provider.
        /// </summary>
        /// <returns>Provider settings.</returns>
        public static ModelProviderSettings CreateOpenAiProvider()
        {
            return new ModelProviderSettings
            {
                Id = "provider_openai",
                Name = "OpenAI",
                Type = ModelProviderTypeEnum.OpenAI,
                Endpoint = "https://api.openai.com",
                Model = "gpt-4o-mini",
                Enabled = false,
                DefaultStreaming = true,
                SupportsNativeToolCalls = true,
                UseNativeToolCalls = true,
                SupportsStrictJson = true,
                ToolCapabilityNote = "OpenAI chat models generally support native tool calls.",
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000,
                MaxConcurrentRequests = 4
            };
        }

        /// <summary>
        /// Create the default OpenAI-compatible provider.
        /// </summary>
        /// <returns>Provider settings.</returns>
        public static ModelProviderSettings CreateOpenAiCompatibleProvider()
        {
            return new ModelProviderSettings
            {
                Id = "provider_openai_compatible",
                Name = "OpenAI Compatible",
                Type = ModelProviderTypeEnum.OpenAICompatible,
                Endpoint = "http://localhost:1234",
                Model = "local-model",
                Enabled = false,
                DefaultStreaming = true,
                SupportsNativeToolCalls = true,
                UseNativeToolCalls = false,
                SupportsStrictJson = false,
                ToolCapabilityNote = "OpenAI-compatible endpoints vary by server and model. Enable native tools after validating the endpoint supports function/tool calls.",
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000,
                MaxConcurrentRequests = 1
            };
        }

        /// <summary>
        /// Create the default Gemini provider.
        /// </summary>
        /// <returns>Provider settings.</returns>
        public static ModelProviderSettings CreateGeminiProvider()
        {
            return new ModelProviderSettings
            {
                Id = "provider_gemini",
                Name = "Gemini",
                Type = ModelProviderTypeEnum.Gemini,
                Endpoint = "https://generativelanguage.googleapis.com",
                Model = "gemini-2.5-flash",
                Enabled = false,
                DefaultStreaming = true,
                SupportsNativeToolCalls = true,
                UseNativeToolCalls = true,
                SupportsStrictJson = true,
                ToolCapabilityNote = "Gemini models support native function calling through PolyPrompt.",
                Temperature = 0.2,
                MaxTokens = 4096,
                RequestTimeoutMs = 120000,
                MaxConcurrentRequests = 4
            };
        }

        /// <summary>
        /// Create the default sample SQLite database connection.
        /// </summary>
        /// <returns>Database settings.</returns>
        public static DatabaseEntry CreateSampleDatabase()
        {
            return new DatabaseEntry
            {
                Id = "db_sample_sqlite",
                Name = "Sample E-Commerce",
                Type = DatabaseTypeEnum.Sqlite,
                Filename = "./database.db",
                DatabaseName = "sample",
                Schema = "main",
                AllowedQueries = new List<string> { "SELECT", "INSERT", "UPDATE", "DELETE" },
                Context = "Sample e-commerce database with three tables. The users table stores customer information. The orders table tracks purchases with a foreign key to users. The line_items table holds individual order items with a foreign key to orders."
            };
        }
    }
}
