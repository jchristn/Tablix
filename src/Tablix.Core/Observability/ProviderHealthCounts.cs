namespace Tablix.Core.Observability
{
    /// <summary>
    /// Snapshot of model provider health counts, reported by the <see cref="TelemetryNames.MetricModelProviders"/> gauge.
    /// </summary>
    public class ProviderHealthCounts
    {
        #region Public-Members

        /// <summary>
        /// Monitored providers currently healthy. Default 0.
        /// </summary>
        public long Healthy { get; set; } = 0;

        /// <summary>
        /// Monitored providers currently unhealthy. Default 0.
        /// </summary>
        public long Unhealthy { get; set; } = 0;

        /// <summary>
        /// Providers that are disabled or have health checks turned off. Default 0.
        /// </summary>
        public long Unmonitored { get; set; } = 0;

        #endregion
    }
}
