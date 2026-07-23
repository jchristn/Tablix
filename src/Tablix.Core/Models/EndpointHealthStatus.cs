namespace Tablix.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Snapshot of model provider health state.
    /// </summary>
    public class EndpointHealthStatus
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
        /// Uptime percentage over observed checks.
        /// </summary>
        public double UptimePercentage { get; set; } = 100;

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
        /// Recent health-check history.
        /// </summary>
        public List<HealthCheckRecord> History { get; set; } = new List<HealthCheckRecord>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EndpointHealthStatus()
        {
        }

        /// <summary>
        /// Build an immutable health status snapshot from state.
        /// </summary>
        /// <param name="state">Health state.</param>
        /// <returns>Health status.</returns>
        public static EndpointHealthStatus FromState(EndpointHealthState state)
        {
            if (state == null) return null;

            lock (state.SyncRoot)
            {
                DateTime now = DateTime.UtcNow;
                long uptime = state.TotalUptimeMs;
                long downtime = state.TotalDowntimeMs;
                if (state.LastCheckUtc.HasValue)
                {
                    long elapsedMs = Math.Max(0, (long)(now - state.LastCheckUtc.Value).TotalMilliseconds);
                    if (state.IsHealthy) uptime += elapsedMs;
                    else downtime += elapsedMs;
                }

                long total = uptime + downtime;

                return new EndpointHealthStatus
                {
                    EndpointId = state.EndpointId,
                    EndpointName = state.EndpointName,
                    HealthCheckEnabled = state.HealthCheckEnabled,
                    IsHealthy = state.IsHealthy,
                    FirstCheckUtc = state.FirstCheckUtc,
                    LastCheckUtc = state.LastCheckUtc,
                    LastHealthyUtc = state.LastHealthyUtc,
                    LastUnhealthyUtc = state.LastUnhealthyUtc,
                    LastStateChangeUtc = state.LastStateChangeUtc,
                    TotalUptimeMs = uptime,
                    TotalDowntimeMs = downtime,
                    UptimePercentage = total == 0 ? 100 : Math.Round((double)uptime / total * 100, 2),
                    ConsecutiveSuccesses = state.ConsecutiveSuccesses,
                    ConsecutiveFailures = state.ConsecutiveFailures,
                    LastError = state.LastError,
                    History = state.CheckHistory.ToList()
                };
            }
        }

        #endregion
    }
}
