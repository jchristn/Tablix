namespace Tablix.Server.Handlers
{
    using Tablix.Core.Models;

    /// <summary>
    /// Model-visible query execution result.
    /// </summary>
    internal class ChatQueryToolResult
    {
        /// <summary>
        /// Whether the query succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Query result.
        /// </summary>
        public QueryResult QueryResult { get; set; } = null;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Whether schema was refreshed.
        /// </summary>
        public bool SchemaRefreshed { get; set; } = false;

        /// <summary>
        /// Schema refresh duration.
        /// </summary>
        public double SchemaRefreshMs { get; set; } = 0;

        /// <summary>
        /// Table count after schema refresh.
        /// </summary>
        public int SchemaRefreshTableCount { get; set; } = 0;

        /// <summary>
        /// Initial error before retry.
        /// </summary>
        public string InitialError { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatQueryToolResult()
        {
        }

        /// <summary>
        /// Create from a chat query execution result.
        /// </summary>
        /// <param name="result">Execution result.</param>
        /// <returns>Tool result.</returns>
        public static ChatQueryToolResult From(ChatQueryExecutionResult result)
        {
            if (result == null) return null;

            return new ChatQueryToolResult
            {
                Success = result.Success,
                QueryResult = result.QueryResult,
                Error = result.Error,
                SchemaRefreshed = result.SchemaRefreshed,
                SchemaRefreshMs = result.SchemaRefreshMs,
                SchemaRefreshTableCount = result.SchemaRefreshTableCount,
                InitialError = result.InitialError
            };
        }
    }
}
