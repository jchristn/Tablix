namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// HTTP methods supported by model provider health checks.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum HealthCheckMethodEnum
    {
        /// <summary>
        /// HTTP GET.
        /// </summary>
        GET,

        /// <summary>
        /// HTTP HEAD.
        /// </summary>
        HEAD
    }
}
