namespace Tablix.Server.Handlers
{
    /// <summary>
    /// Effective chat execution policy for one request.
    /// </summary>
    internal class ChatExecutionPolicy
    {
        /// <summary>
        /// Whether chat tools are enabled.
        /// </summary>
        public bool ToolsEnabled { get; set; } = false;

        /// <summary>
        /// Whether native provider tools are preferred.
        /// </summary>
        public bool PreferNativeTools { get; set; } = false;

        /// <summary>
        /// Whether server fallback execution is enabled.
        /// </summary>
        public bool FallbackEnabled { get; set; } = false;

        /// <summary>
        /// Whether native tools should be used for this request.
        /// </summary>
        public bool UseNativeTools { get; set; } = false;

        /// <summary>
        /// Whether server-side model planning should decide if a query should run.
        /// </summary>
        public bool UseFallbackPlanner { get; set; } = false;

        /// <summary>
        /// Maximum native tool iterations.
        /// </summary>
        public int MaxNativeToolIterations { get; set; } = 4;

        /// <summary>
        /// Maximum planning attempts.
        /// </summary>
        public int MaxPlanningAttempts { get; set; } = 2;

        /// <summary>
        /// UI capability notice.
        /// </summary>
        public string CapabilityNotice { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatExecutionPolicy()
        {
        }
    }
}
