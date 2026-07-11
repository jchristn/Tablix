namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tablix product-state persistence database type.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TablixPersistenceDatabaseTypeEnum
    {
        /// <summary>
        /// SQLite single-file persistence database.
        /// </summary>
        Sqlite
    }
}
