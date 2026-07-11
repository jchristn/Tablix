namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Response from generated table context persistence.
    /// </summary>
    public class BuildTableContextResponse
    {
        #region Public-Members

        /// <summary>
        /// Whether table context was generated and saved.
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
        /// Model used by the provider.
        /// </summary>
        public string Model { get; set; } = null;

        /// <summary>
        /// Generated and persisted table context records.
        /// </summary>
        public List<TableContextRead> Objects { get; set; } = new List<TableContextRead>();

        /// <summary>
        /// Aggregate generation telemetry.
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
        public BuildTableContextResponse()
        {
        }

        #endregion
    }
}
