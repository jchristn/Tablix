namespace Tablix.Core.Models
{
    /// <summary>
    /// Database connectivity test response.
    /// </summary>
    public class DatabaseConnectivityTestResponse
    {
        /// <summary>
        /// Whether the connectivity test succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Database identifier, when known.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Sanitized test message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Sanitized error message.
        /// </summary>
        public string Error { get; set; } = null;

        /// <summary>
        /// Runtime in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseConnectivityTestResponse()
        {
        }
    }
}
