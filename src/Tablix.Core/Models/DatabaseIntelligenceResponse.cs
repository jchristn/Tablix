namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Whole-database intelligence derived from schema and context.
    /// </summary>
    public class DatabaseIntelligenceResponse
    {
        #region Public-Members

        /// <summary>
        /// Whether the response succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Domain interpretation.
        /// </summary>
        public DomainIntelligence Domain { get; set; } = null;

        /// <summary>
        /// Declared and inferred relationships.
        /// </summary>
        public List<RelationshipDetail> Relationships
        {
            get { return _Relationships; }
            set { _Relationships = value ?? new List<RelationshipDetail>(); }
        }

        /// <summary>
        /// Ambiguities that agents should clarify before executing SQL.
        /// </summary>
        public List<AmbiguitySignal> Ambiguities
        {
            get { return _Ambiguities; }
            set { _Ambiguities = value ?? new List<AmbiguitySignal>(); }
        }

        /// <summary>
        /// Context quality score.
        /// </summary>
        public ContextQualityScore ContextQuality { get; set; } = null;

        /// <summary>
        /// Agent pack for this database.
        /// </summary>
        public AgentPackResponse AgentPack { get; set; } = null;

        /// <summary>
        /// Total processing time in milliseconds.
        /// </summary>
        public double TotalMs { get; set; } = 0;

        #endregion

        #region Private-Members

        private List<RelationshipDetail> _Relationships = new List<RelationshipDetail>();
        private List<AmbiguitySignal> _Ambiguities = new List<AmbiguitySignal>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseIntelligenceResponse()
        {
        }

        #endregion
    }
}
