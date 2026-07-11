namespace Tablix.Core.Settings
{
    using System;

    /// <summary>
    /// Settings that control chat prompt processing and tool execution policy.
    /// </summary>
    public class PromptProcessingSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable prompt processing orchestration for chat.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Prefer provider-native PolyPrompt tool calls when the selected provider supports them.
        /// </summary>
        public bool PreferNativeToolCalls { get; set; } = true;

        /// <summary>
        /// Execute a permitted query when the user clearly asks for data and execution is available.
        /// </summary>
        public bool RequireExecutionForDataRequests { get; set; } = true;

        /// <summary>
        /// Allow SQL-only answers when the user explicitly asks for SQL or says not to execute.
        /// </summary>
        public bool AllowSqlOnlyByExplicitRequest { get; set; } = true;

        /// <summary>
        /// Use server-side fallback planning when native tool calls are unavailable or omitted.
        /// </summary>
        public bool FallbackWhenNativeToolNotCalled { get; set; } = true;

        /// <summary>
        /// Refresh schema and retry once when query execution fails due to stale column metadata.
        /// </summary>
        public bool RetryAfterSchemaRefresh { get; set; } = true;

        /// <summary>
        /// Maximum native tool iterations. Values are clamped from 1 to 25.
        /// </summary>
        public int MaxNativeToolIterations
        {
            get { return _MaxNativeToolIterations; }
            set { _MaxNativeToolIterations = Math.Clamp(value, 1, 25); }
        }

        /// <summary>
        /// Maximum fallback planning attempts. Values are clamped from 1 to 5.
        /// </summary>
        public int MaxPlanningAttempts
        {
            get { return _MaxPlanningAttempts; }
            set { _MaxPlanningAttempts = Math.Clamp(value, 1, 5); }
        }

        /// <summary>
        /// Fallback planner temperature. Values are clamped from 0.0 to 2.0.
        /// </summary>
        public double PlannerTemperature
        {
            get { return _PlannerTemperature; }
            set { _PlannerTemperature = Math.Clamp(value, 0.0, 2.0); }
        }

        #endregion

        #region Private-Members

        private int _MaxNativeToolIterations = 4;
        private int _MaxPlanningAttempts = 2;
        private double _PlannerTemperature = 0.0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PromptProcessingSettings()
        {
        }

        #endregion
    }
}
