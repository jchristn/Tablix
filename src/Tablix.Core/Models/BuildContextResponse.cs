namespace Tablix.Core.Models
{
    /// <summary>
    /// Response from generated database context persistence.
    /// </summary>
    public class BuildContextResponse
    {
        #region Public-Members

        /// <summary>
        /// Whether context was generated and saved.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Provider identifier.
        /// </summary>
        public string ProviderId { get; set; } = null;

        /// <summary>
        /// Generated context persisted to settings.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Model used by the provider.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Generation telemetry.
        /// </summary>
        public ChatTelemetry Telemetry { get; set; } = null;

        /// <summary>
        /// Error message when unsuccessful.
        /// </summary>
        public string Error { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BuildContextResponse()
        {
        }

        #endregion
    }
}
