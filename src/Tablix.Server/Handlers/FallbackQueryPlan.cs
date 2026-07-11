namespace Tablix.Server.Handlers
{
    /// <summary>
    /// Strict fallback planner response.
    /// </summary>
    internal class FallbackQueryPlan
    {
        /// <summary>
        /// Whether a query should be executed.
        /// </summary>
        public bool Execute { get; set; } = false;

        /// <summary>
        /// Query to execute.
        /// </summary>
        public string Query { get; set; } = null;

        /// <summary>
        /// Reason for the plan.
        /// </summary>
        public string Reason { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FallbackQueryPlan()
        {
        }
    }
}
