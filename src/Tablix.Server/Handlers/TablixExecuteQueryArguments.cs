namespace Tablix.Server.Handlers
{
    /// <summary>
    /// Arguments for the native Tablix query execution tool.
    /// </summary>
    internal class TablixExecuteQueryArguments
    {
        /// <summary>
        /// Selected database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// SQL query.
        /// </summary>
        public string Query { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TablixExecuteQueryArguments()
        {
        }
    }
}
