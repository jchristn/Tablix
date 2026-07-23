namespace Tablix.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Mutable in-memory model provider health state.
    /// </summary>
    public class EndpointHealthState
    {
        #region Public-Members

        /// <summary>
        /// Endpoint identifier.
        /// </summary>
        public string EndpointId { get; set; } = null;

        /// <summary>
        /// Endpoint display name.
        /// </summary>
        public string EndpointName { get; set; } = null;

        /// <summary>
        /// Whether health checks are active.
        /// </summary>
        public bool HealthCheckEnabled { get; set; } = true;

        /// <summary>
        /// Whether the endpoint is currently healthy.
        /// </summary>
        public bool IsHealthy { get; set; } = true;

        /// <summary>
        /// First check timestamp.
        /// </summary>
        public DateTime? FirstCheckUtc { get; set; } = null;

        /// <summary>
        /// Last check timestamp.
        /// </summary>
        public DateTime? LastCheckUtc { get; set; } = null;

        /// <summary>
        /// Last healthy timestamp.
        /// </summary>
        public DateTime? LastHealthyUtc { get; set; } = null;

        /// <summary>
        /// Last unhealthy timestamp.
        /// </summary>
        public DateTime? LastUnhealthyUtc { get; set; } = null;

        /// <summary>
        /// Last state-change timestamp.
        /// </summary>
        public DateTime? LastStateChangeUtc { get; set; } = null;

        /// <summary>
        /// Accumulated uptime in milliseconds.
        /// </summary>
        public long TotalUptimeMs { get; set; } = 0;

        /// <summary>
        /// Accumulated downtime in milliseconds.
        /// </summary>
        public long TotalDowntimeMs { get; set; } = 0;

        /// <summary>
        /// Consecutive successful checks.
        /// </summary>
        public int ConsecutiveSuccesses { get; set; } = 0;

        /// <summary>
        /// Consecutive failed checks.
        /// </summary>
        public int ConsecutiveFailures { get; set; } = 0;

        /// <summary>
        /// Last health-check error.
        /// </summary>
        public string LastError { get; set; } = null;

        /// <summary>
        /// Recent check history.
        /// </summary>
        public List<HealthCheckRecord> CheckHistory { get; } = new List<HealthCheckRecord>();

        /// <summary>
        /// Synchronization root.
        /// </summary>
        public object SyncRoot { get; } = new object();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EndpointHealthState()
        {
        }

        #endregion
    }
}
