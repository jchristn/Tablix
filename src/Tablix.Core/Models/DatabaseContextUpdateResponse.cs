namespace Tablix.Core.Models
{
    /// <summary>
    /// Response from a database context update request.
    /// </summary>
    public class DatabaseContextUpdateResponse
    {
        /// <summary>
        /// Whether the update succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Saved database context.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Update mode applied to the context.
        /// </summary>
        public string Mode { get; set; } = null;
    }
}
