namespace Tablix.Core.Models
{
    /// <summary>
    /// Quality signal for database or table context readiness.
    /// </summary>
    public class ContextQualitySignal
    {
        #region Public-Members

        /// <summary>
        /// Signal key.
        /// </summary>
        public string Key { get; set; } = null;

        /// <summary>
        /// Signal severity: info, warning, or blocker.
        /// </summary>
        public string Severity { get; set; } = "info";

        /// <summary>
        /// Signal message.
        /// </summary>
        public string Message { get; set; } = null;

        /// <summary>
        /// Recommended improvement.
        /// </summary>
        public string Recommendation { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ContextQualitySignal()
        {
        }

        #endregion
    }
}
