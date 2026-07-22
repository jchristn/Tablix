namespace Tablix.Server.Handlers
{
    /// <summary>
    /// Arguments for the native Tablix table context update tool.
    /// </summary>
    internal class TablixUpdateTableContextArguments
    {
        /// <summary>
        /// Selected database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Persisted table metadata identifier.
        /// </summary>
        public string TableId { get; set; } = null;

        /// <summary>
        /// Schema name used with table name when table ID is not available.
        /// </summary>
        public string SchemaName { get; set; } = null;

        /// <summary>
        /// Exact table name used when table ID is not available.
        /// </summary>
        public string TableName { get; set; } = null;

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
        public TablixUpdateTableContextArguments()
        {
        }
    }
}
