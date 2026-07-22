namespace Tablix.Server.Handlers
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Result returned to the model after a chat context update tool call.
    /// </summary>
    internal class ChatContextUpdateToolResult
    {
        /// <summary>
        /// Whether the update succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Updated context scope.
        /// </summary>
        public ContextScopeEnum Scope { get; set; } = ContextScopeEnum.Database;

        /// <summary>
        /// Selected database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Persisted table metadata identifier for table-scoped updates.
        /// </summary>
        public string TableId { get; set; } = null;

        /// <summary>
        /// Schema name for table-scoped updates.
        /// </summary>
        public string SchemaName { get; set; } = null;

        /// <summary>
        /// Table name for table-scoped updates.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Applied update mode.
        /// </summary>
        public string Mode { get; set; } = null;

        /// <summary>
        /// Saved context after the update.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Brief reason supplied by the model.
        /// </summary>
        public string Reason { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatContextUpdateToolResult()
        {
        }
    }
}
