namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Supported database types.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// SQLite.
        /// </summary>
        Sqlite,

        /// <summary>
        /// PostgreSQL.
        /// </summary>
        Postgresql,

        /// <summary>
        /// MySQL.
        /// </summary>
        Mysql,

        /// <summary>
        /// SQL Server.
        /// </summary>
        SqlServer
    }
}
