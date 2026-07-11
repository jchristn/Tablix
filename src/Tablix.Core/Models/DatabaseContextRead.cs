namespace Tablix.Core.Models
{
    /// <summary>
    /// Persisted database-level context read model.
    /// </summary>
    public class DatabaseContextRead
    {
        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Database display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Database engine type.
        /// </summary>
        public string Type { get; set; } = null;

        /// <summary>
        /// Saved database-level context.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseContextRead()
        {
        }
    }
}
