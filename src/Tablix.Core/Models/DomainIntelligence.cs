namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Heuristic domain interpretation derived from crawled schema and saved context.
    /// </summary>
    public class DomainIntelligence
    {
        #region Public-Members

        /// <summary>
        /// Short domain summary.
        /// </summary>
        public string Summary { get; set; } = null;

        /// <summary>
        /// Main domain entities.
        /// </summary>
        public List<DomainEntity> Entities
        {
            get { return _Entities; }
            set { _Entities = value ?? new List<DomainEntity>(); }
        }

        /// <summary>
        /// Likely workflow or join paths.
        /// </summary>
        public List<string> Workflows
        {
            get { return _Workflows; }
            set { _Workflows = value ?? new List<string>(); }
        }

        /// <summary>
        /// Candidate metric columns.
        /// </summary>
        public List<string> Metrics
        {
            get { return _Metrics; }
            set { _Metrics = value ?? new List<string>(); }
        }

        /// <summary>
        /// Common filter columns such as status, tenant, date, and soft-delete fields.
        /// </summary>
        public List<string> CommonFilters
        {
            get { return _CommonFilters; }
            set { _CommonFilters = value ?? new List<string>(); }
        }

        /// <summary>
        /// Timestamp or freshness columns.
        /// </summary>
        public List<string> FreshnessColumns
        {
            get { return _FreshnessColumns; }
            set { _FreshnessColumns = value ?? new List<string>(); }
        }

        /// <summary>
        /// Tenant or account scoping columns.
        /// </summary>
        public List<string> TenantColumns
        {
            get { return _TenantColumns; }
            set { _TenantColumns = value ?? new List<string>(); }
        }

        /// <summary>
        /// Soft-delete columns.
        /// </summary>
        public List<string> SoftDeleteColumns
        {
            get { return _SoftDeleteColumns; }
            set { _SoftDeleteColumns = value ?? new List<string>(); }
        }

        #endregion

        #region Private-Members

        private List<DomainEntity> _Entities = new List<DomainEntity>();
        private List<string> _Workflows = new List<string>();
        private List<string> _Metrics = new List<string>();
        private List<string> _CommonFilters = new List<string>();
        private List<string> _FreshnessColumns = new List<string>();
        private List<string> _TenantColumns = new List<string>();
        private List<string> _SoftDeleteColumns = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DomainIntelligence()
        {
        }

        #endregion
    }
}
