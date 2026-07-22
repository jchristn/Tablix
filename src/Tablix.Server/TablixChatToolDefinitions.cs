namespace Tablix.Server
{
    using System.Collections.Generic;
    using PolyPrompt.Models;

    /// <summary>
    /// PolyPrompt tool definitions exposed to model-backed database chat.
    /// </summary>
    public static class TablixChatToolDefinitions
    {
        #region Public-Members

        /// <summary>
        /// Query execution tool name.
        /// </summary>
        public const string ExecuteQueryToolName = "tablix_execute_query";

        /// <summary>
        /// Database context update tool name.
        /// </summary>
        public const string UpdateDatabaseContextToolName = "tablix_update_database_context";

        /// <summary>
        /// Table context update tool name.
        /// </summary>
        public const string UpdateTableContextToolName = "tablix_update_table_context";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build all chat tool definitions.
        /// </summary>
        /// <returns>Tool definitions.</returns>
        public static List<ToolDefinition> Build()
        {
            return Build(true);
        }

        /// <summary>
        /// Build chat tool definitions.
        /// </summary>
        /// <param name="allowContextUpdates">Whether context update tools should be exposed.</param>
        /// <returns>Tool definitions.</returns>
        public static List<ToolDefinition> Build(bool allowContextUpdates)
        {
            return new List<ToolDefinition>
            {
                BuildExecuteQueryTool(),
                BuildUpdateDatabaseContextToolIfAllowed(allowContextUpdates),
                BuildUpdateTableContextToolIfAllowed(allowContextUpdates)
            }
            .FindAll(tool => tool != null);
        }

        /// <summary>
        /// Build the query execution tool definition.
        /// </summary>
        /// <returns>Tool definition.</returns>
        public static ToolDefinition BuildExecuteQueryTool()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                { "DatabaseId", BuildDatabaseIdProperty() },
                { "Query", BuildQueryProperty() }
            };

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "additionalProperties", false },
                { "properties", properties },
                { "required", new List<string> { "DatabaseId", "Query" } }
            };

            return ToolDefinition.Function(
                ExecuteQueryToolName,
                "Execute one permitted SQL statement against the selected database when the user asks for database data, database contents, row examples, counts, totals, or an explicit database action. Call this tool instead of merely returning SQL or asking the user to run SQL when execution can answer the user. Return SQL text only when the user explicitly asks for SQL only or execution is unavailable.",
                parameters);
        }

        /// <summary>
        /// Build the database context update tool definition.
        /// </summary>
        /// <returns>Tool definition.</returns>
        public static ToolDefinition BuildUpdateDatabaseContextTool()
        {
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                { "DatabaseId", BuildDatabaseIdProperty() },
                { "Context", BuildDatabaseContextProperty() },
                { "Mode", BuildModeProperty() },
                { "Reason", BuildReasonProperty() }
            };

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "additionalProperties", false },
                { "properties", properties },
                { "required", new List<string> { "DatabaseId", "Context", "Mode" } }
            };

            return ToolDefinition.Function(
                UpdateDatabaseContextToolName,
                "Update persisted database-level context for the selected database when tool results or schema inspection reveal a durable, reusable database-wide relationship, business rule, naming convention, or correction. Use append for incremental observations. Use replace only when explicitly asked to rewrite the saved context or when producing a curated complete replacement. Do not store secrets, credentials, raw result rows, sensitive personal data, unsupported guesses, or one-off answer values. Clearly label inferred relationships.",
                parameters);
        }

        /// <summary>
        /// Build the table context update tool definition.
        /// </summary>
        /// <returns>Tool definition.</returns>
        public static ToolDefinition BuildUpdateTableContextTool()
        {
            Dictionary<string, object> tableIdProperty = new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "The persisted table metadata ID from the schema context. Prefer this over table name when available." }
            };

            Dictionary<string, object> schemaNameProperty = new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "Optional schema name for table-name resolution when TableId is not available." }
            };

            Dictionary<string, object> tableNameProperty = new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "Exact table name from the schema context when TableId is not available. You may use schema.table in this field if SchemaName is omitted." }
            };

            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                { "DatabaseId", BuildDatabaseIdProperty() },
                { "TableId", tableIdProperty },
                { "SchemaName", schemaNameProperty },
                { "TableName", tableNameProperty },
                { "Context", BuildTableContextProperty() },
                { "Mode", BuildModeProperty() },
                { "Reason", BuildReasonProperty() }
            };

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "type", "object" },
                { "additionalProperties", false },
                { "properties", properties },
                { "required", new List<string> { "DatabaseId", "Context", "Mode" } }
            };

            return ToolDefinition.Function(
                UpdateTableContextToolName,
                "Update persisted table-level context for the selected database when tool results or schema inspection reveal durable, reusable facts about one exact table, such as purpose, join paths, relationships, important filters, write-safety caveats, or corrections. Use append for incremental observations. Use replace only when explicitly asked to rewrite that table context or when producing a curated complete replacement. Do not store secrets, credentials, raw result rows, sensitive personal data, unsupported guesses, or one-off answer values. Clearly label inferred relationships.",
                parameters);
        }

        #endregion

        #region Private-Methods

        private static ToolDefinition BuildUpdateDatabaseContextToolIfAllowed(bool allowContextUpdates)
        {
            if (!allowContextUpdates) return null;
            return BuildUpdateDatabaseContextTool();
        }

        private static ToolDefinition BuildUpdateTableContextToolIfAllowed(bool allowContextUpdates)
        {
            if (!allowContextUpdates) return null;
            return BuildUpdateTableContextTool();
        }

        private static Dictionary<string, object> BuildDatabaseIdProperty()
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "The selected Tablix database ID. Use the DatabaseId shown in the system context; do not switch databases." }
            };
        }

        private static Dictionary<string, object> BuildQueryProperty()
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "One SQL statement to execute. The statement type must be allowed by the selected database, and the SQL must not contain semicolons." }
            };
        }

        private static Dictionary<string, object> BuildModeProperty()
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "enum", new List<string> { "append", "replace" } },
                { "description", "Context update mode. Use append for new durable observations. Use replace only for an explicit full rewrite." }
            };
        }

        private static Dictionary<string, object> BuildDatabaseContextProperty()
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "Concise database-level context to save. Include only durable, reusable information about the whole database; do not include raw result rows, secrets, credentials, or unsupported guesses." }
            };
        }

        private static Dictionary<string, object> BuildTableContextProperty()
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "Concise table-level context to save. Include only durable, reusable information about the exact table; do not include raw result rows, secrets, credentials, or unsupported guesses." }
            };
        }

        private static Dictionary<string, object> BuildReasonProperty()
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "Brief reason this context update is durable and should be persisted." }
            };
        }

        #endregion
    }
}
