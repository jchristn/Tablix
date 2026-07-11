namespace Tablix.Core.Persistence.Sqlite
{
    /// <summary>
    /// Internal persisted table row used while rebuilding table metadata.
    /// </summary>
    internal class SqliteTableRow
    {
        /// <summary>
        /// Persisted table identifier.
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
        /// Table context.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SqliteTableRow()
        {
        }
    }
}
