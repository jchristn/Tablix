namespace Tablix.Core.Persistence
{
    using System.Collections.Generic;

    /// <summary>
    /// Versioned persistence schema migration.
    /// </summary>
    public class SchemaMigration
    {
        /// <summary>
        /// Migration version.
        /// </summary>
        public int Version { get; set; } = 0;

        /// <summary>
        /// Migration description.
        /// </summary>
        public string Description { get; set; } = null;

        /// <summary>
        /// SQL statements to execute.
        /// </summary>
        public List<string> Statements
        {
            get { return _Statements; }
            set { _Statements = value ?? new List<string>(); }
        }

        private List<string> _Statements = new List<string>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SchemaMigration()
        {
        }
    }
}
