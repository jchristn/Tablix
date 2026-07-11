namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Scope for persisted Tablix context records.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ContextScopeEnum
    {
        /// <summary>
        /// Context applies to an entire configured database.
        /// </summary>
        Database,

        /// <summary>
        /// Context applies to one table in a configured database.
        /// </summary>
        Table
    }
}
