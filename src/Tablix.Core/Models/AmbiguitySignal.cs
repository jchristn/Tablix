namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Ambiguous database term or query intent detected from schema and context.
    /// </summary>
    public class AmbiguitySignal
    {
        #region Public-Members

        /// <summary>
        /// Ambiguous concept, such as revenue, active, latest, or status.
        /// </summary>
        public string Term { get; set; } = null;

        /// <summary>
        /// Why the term is ambiguous.
        /// </summary>
        public string Reason { get; set; } = null;

        /// <summary>
        /// Clarifying question to ask before executing SQL.
        /// </summary>
        public string Question { get; set; } = null;

        /// <summary>
        /// Candidate schema interpretations.
        /// </summary>
        public List<string> Candidates
        {
            get { return _Candidates; }
            set { _Candidates = value ?? new List<string>(); }
        }

        #endregion

        #region Private-Members

        private List<string> _Candidates = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AmbiguitySignal()
        {
        }

        #endregion
    }
}
