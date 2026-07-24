namespace Tablix.Core.Models
{
    using System;

    /// <summary>
    /// REST service health response.
    /// </summary>
    public class HealthStatusResponse
    {
        /// <summary>
        /// Product name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Product version.
        /// </summary>
        public string Version { get; set; } = null;

        /// <summary>
        /// UTC timestamp when the service started.
        /// </summary>
        public DateTime StartTimeUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Current service uptime.
        /// </summary>
        public TimeSpan Uptime { get; set; } = TimeSpan.Zero;
    }
}
