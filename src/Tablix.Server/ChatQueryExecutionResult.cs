namespace Tablix.Server
{
    using Tablix.Core.Models;

    /// <summary>
    /// Query execution result for chat tooling.
    /// </summary>
    public class ChatQueryExecutionResult
    {
        /// <summary>
        /// Whether execution succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Query result.
        /// </summary>
        public QueryResult QueryResult { get; set; } = null;

        /// <summary>
        /// Validation error.
        /// </summary>
        public string ValidationError { get; set; } = null;

        /// <summary>
        /// Error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Execution duration.
        /// </summary>
        public double TotalMs { get; set; } = 0;

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
        public ChatQueryExecutionResult()
        {
        }
    }
}
