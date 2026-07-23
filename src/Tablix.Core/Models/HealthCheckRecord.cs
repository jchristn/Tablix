namespace Tablix.Core.Models
{
    using System;

    /// <summary>
    /// One model provider health check observation.
    /// </summary>
    public class HealthCheckRecord
    {
        #region Public-Members

        /// <summary>
        /// Timestamp in UTC.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the check succeeded.
        /// </summary>
        public bool Success { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public HealthCheckRecord()
        {
        }

        #endregion
    }
}
