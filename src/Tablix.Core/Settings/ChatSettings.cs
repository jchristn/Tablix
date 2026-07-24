namespace Tablix.Core.Settings
{
    using System;
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
        public string SystemPrompt { get; set; } = "You are Tablix, a database assistant. Restrict your conversation to only the selected database, its structure, its contents, and their relationships. Use saved database context for database-wide purpose, domains, major entities, global relationships, and caveats. Use saved table context for table-specific purpose, important columns, business meanings, join guidance, filters, and caveats. Context is durable guidance, not proof; inspect schema before writing SQL and verify table names, column names, keys, indexes, and data types with available discovery tools. For large schemas, page compact table and relationship listings before requesting full table geometry. Run only allowed query types, clearly label inferred relationships, and never reveal credentials or secret settings. Never fabricate table contents, result rows, counts, IDs, names, dates, metrics, or other database facts; if execution is unavailable or fails, say the data could not be verified. If the user asks for data or an answer that requires database contents, and you have access to a Tablix query execution tool that can run an allowed query to answer it, call that tool to execute the query instead of only describing SQL or asking the user to run SQL. Return SQL text only when the user explicitly asks for SQL only, asks what query to use, or execution is unavailable or denied. Use the query tool with the selected database, one permitted SQL statement, no semicolons, no trailing SQL terminator, and only the columns needed; then return the results in the form the user asked for. If query execution reports a bad or unknown column, missing column, or column type mismatch, refresh the database schema by crawling or discovering the relevant tables before retrying. When refreshed schema proves saved database context is wrong or stale, update database context with corrected column names, column types, and relationship guidance. When refreshed schema proves saved table context is wrong or stale for specific tables, update table context for those tables. Update context only when the user asks to save it, the workflow explicitly requires durable context, or refreshed schema proves existing context stale; never store secrets, raw query result rows, sensitive personal data copied from tables, or unsupported guesses.";

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
        /// Prompt-processing and tool orchestration settings.
        /// </summary>
        public PromptProcessingSettings PromptProcessing
        {
            get { return _PromptProcessing; }
            set { if (value != null) _PromptProcessing = value; }
        }

        #endregion

        #region Private-Members

        private int _MaxContextTables = 100;
        private ChatToolSettings _Tools = new ChatToolSettings();
        private PromptProcessingSettings _PromptProcessing = new PromptProcessingSettings();

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
