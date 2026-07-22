namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Verification metadata for a chat answer.
    /// </summary>
    public class VerifiedAnswer
    {
        #region Public-Members

        /// <summary>
        /// Verification state: verified, partial, blocked, or ambiguous.
        /// </summary>
        public string State { get; set; } = "partial";

        /// <summary>
        /// Human-readable verification summary.
        /// </summary>
        public string Summary { get; set; } = null;

        /// <summary>
        /// SQL statement used to verify the answer, when available.
        /// </summary>
        public string Sql { get; set; } = null;

        /// <summary>
        /// Tool call identifier that produced the evidence.
        /// </summary>
        public string ToolCallId { get; set; } = null;

        /// <summary>
        /// Number of rows returned by the verification query.
        /// </summary>
        public int? RowsReturned { get; set; } = null;

        /// <summary>
        /// Evidence statements supporting the verification state.
        /// </summary>
        public List<string> Evidence
        {
            get { return _Evidence; }
            set { _Evidence = value ?? new List<string>(); }
        }

        /// <summary>
        /// Error or blocker detail.
        /// </summary>
        public string Error { get; set; } = null;

        #endregion

        #region Private-Members

        private List<string> _Evidence = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public VerifiedAnswer()
        {
        }

        #endregion
    }
}
