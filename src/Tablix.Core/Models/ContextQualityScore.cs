namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Score and signals describing how ready saved context is for agent use.
    /// </summary>
    public class ContextQualityScore
    {
        #region Public-Members

        /// <summary>
        /// Score from 0 to 100.
        /// </summary>
        public int Score { get; set; } = 0;

        /// <summary>
        /// Score label.
        /// </summary>
        public string Label { get; set; } = null;

        /// <summary>
        /// Tables with saved context.
        /// </summary>
        public int TablesWithContext { get; set; } = 0;

        /// <summary>
        /// Total tables in the crawl.
        /// </summary>
        public int TotalTables { get; set; } = 0;

        /// <summary>
        /// Declared foreign-key relationships.
        /// </summary>
        public int DeclaredRelationships { get; set; } = 0;

        /// <summary>
        /// Inferred relationship candidates.
        /// </summary>
        public int InferredRelationships { get; set; } = 0;

        /// <summary>
        /// Quality signals.
        /// </summary>
        public List<ContextQualitySignal> Signals
        {
            get { return _Signals; }
            set { _Signals = value ?? new List<ContextQualitySignal>(); }
        }

        #endregion

        #region Private-Members

        private List<ContextQualitySignal> _Signals = new List<ContextQualitySignal>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ContextQualityScore()
        {
        }

        #endregion
    }
}
