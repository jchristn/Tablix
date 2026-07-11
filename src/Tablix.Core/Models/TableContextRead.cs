namespace Tablix.Core.Models
{
    using System;

    /// <summary>
    /// Persisted table context.
    /// </summary>
    public class TableContextRead
    {
        /// <summary>
        /// Context record identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Table metadata identifier.
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
        /// Context text.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Context source.
        /// </summary>
        public string Source { get; set; } = null;

        /// <summary>
        /// Last updated UTC timestamp.
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TableContextRead()
        {
        }
    }
}
