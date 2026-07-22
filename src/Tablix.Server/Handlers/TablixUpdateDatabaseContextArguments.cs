namespace Tablix.Server.Handlers
{
    /// <summary>
    /// Arguments for the native Tablix database context update tool.
    /// </summary>
    internal class TablixUpdateDatabaseContextArguments
    {
        /// <summary>
        /// Selected database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Context text to persist.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Update mode. Supported values are append and replace.
        /// </summary>
        public string Mode { get; set; } = "append";

        /// <summary>
        /// Brief reason the context should be persisted.
        /// </summary>
        public string Reason { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TablixUpdateDatabaseContextArguments()
        {
        }
    }
}
