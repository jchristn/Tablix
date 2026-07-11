namespace Tablix.Core.Models
{
    /// <summary>
    /// Provider connectivity test response.
    /// </summary>
    public class ProviderConnectivityTestResponse
    {
        /// <summary>
        /// Whether the connectivity test succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Provider identifier, when known.
        /// </summary>
        public string ProviderId { get; set; } = null;

        /// <summary>
        /// Provider model name.
        /// </summary>
        public string Model { get; set; } = null;

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
        public ProviderConnectivityTestResponse()
        {
        }
    }
}
