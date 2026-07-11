namespace Tablix.Core.Models
{
    /// <summary>
    /// Compact relationship edge between two database tables.
    /// </summary>
    public class RelationshipDetail
    {
        #region Public-Members

        /// <summary>
        /// Source schema name.
        /// </summary>
        public string FromSchema { get; set; } = null;

        /// <summary>
        /// Source table name.
        /// </summary>
        public string FromTable { get; set; } = null;

        /// <summary>
        /// Source column name.
        /// </summary>
        public string FromColumn { get; set; } = null;

        /// <summary>
        /// Referenced schema name, when known.
        /// </summary>
        public string ToSchema { get; set; } = null;

        /// <summary>
        /// Referenced table name.
        /// </summary>
        public string ToTable { get; set; } = null;

        /// <summary>
        /// Referenced column name.
        /// </summary>
        public string ToColumn { get; set; } = null;

        /// <summary>
        /// Relationship constraint name, when available.
        /// </summary>
        public string ConstraintName { get; set; } = null;

        /// <summary>
        /// Relationship source, such as declared_fk.
        /// </summary>
        public string Source { get; set; } = "declared_fk";

        /// <summary>
        /// Confidence from 0.0 to 1.0. Declared foreign keys use 1.0.
        /// </summary>
        public double Confidence { get; set; } = 1.0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RelationshipDetail()
        {
        }

        #endregion
    }
}
