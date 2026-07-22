namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Domain-facing interpretation of a discovered table.
    /// </summary>
    public class DomainEntity
    {
        #region Public-Members

        /// <summary>
        /// Persisted table metadata identifier.
        /// </summary>
        public string TableId { get; set; } = null;

        /// <summary>
        /// Schema name.
        /// </summary>
        public string SchemaName { get; set; } = null;

        /// <summary>
        /// Table name.
        /// </summary>
        public string TableName { get; set; } = null;

        /// <summary>
        /// Inferred role, such as entity, event, lookup, or junction.
        /// </summary>
        public string Role { get; set; } = null;

        /// <summary>
        /// Short explanation of why this table matters.
        /// </summary>
        public string Summary { get; set; } = null;

        /// <summary>
        /// Important columns for agent reasoning.
        /// </summary>
        public List<string> KeyColumns
        {
            get { return _KeyColumns; }
            set { _KeyColumns = value ?? new List<string>(); }
        }

        /// <summary>
        /// Whether table context exists.
        /// </summary>
        public bool HasContext { get; set; } = false;

        #endregion

        #region Private-Members

        private List<string> _KeyColumns = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DomainEntity()
        {
        }

        #endregion
    }
}
