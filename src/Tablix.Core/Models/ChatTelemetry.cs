namespace Tablix.Core.Models
{
    /// <summary>
    /// Chat response telemetry.
    /// </summary>
    public class ChatTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Time to first token in milliseconds.
        /// </summary>
        public long? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Total generation or streaming time in milliseconds.
        /// </summary>
        public long? TotalStreamingTimeMs { get; set; } = null;

        /// <summary>
        /// Input token count, provider-reported or estimated.
        /// </summary>
        public int? InputTokens { get; set; } = null;

        /// <summary>
        /// Output token count, provider-reported or estimated.
        /// </summary>
        public int? OutputTokens { get; set; } = null;

        /// <summary>
        /// Total token count, provider-reported or estimated.
        /// </summary>
        public int? TotalTokens { get; set; } = null;

        /// <summary>
        /// True when token counts are estimated by Tablix.
        /// </summary>
        public bool EstimatedTokens { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatTelemetry()
        {
        }

        #endregion
    }
}
