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

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build all chat tool definitions.
        /// </summary>
        /// <returns>Tool definitions.</returns>
        public static List<ToolDefinition> Build()
        {
            return new List<ToolDefinition>
            {
                BuildExecuteQueryTool()
            };
        }

        /// <summary>
        /// Build the query execution tool definition.
        /// </summary>
        /// <returns>Tool definition.</returns>
        public static ToolDefinition BuildExecuteQueryTool()
        {
            Dictionary<string, object> databaseIdProperty = new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "The selected Tablix database ID. Use the DatabaseId shown in the system context; do not switch databases." }
            };

            Dictionary<string, object> queryProperty = new Dictionary<string, object>
            {
                { "type", "string" },
                { "description", "One SQL statement to execute. The statement type must be allowed by the selected database, and the SQL must not contain semicolons." }
            };

            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                { "DatabaseId", databaseIdProperty },
                { "Query", queryProperty }
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
                "Execute one permitted SQL statement against the selected database when the user asks for database data or an explicit database action. Use this instead of merely returning SQL when execution can answer the user.",
                parameters);
        }

        #endregion
    }
}
