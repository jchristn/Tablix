namespace Tablix.Server.Handlers
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Strict fallback planner response.
    /// </summary>
    internal class FallbackQueryPlan
    {
        /// <summary>
        /// Model-classified user intent.
        /// </summary>
        public PromptIntentTypeEnum Intent { get; set; } = PromptIntentTypeEnum.Unknown;

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
