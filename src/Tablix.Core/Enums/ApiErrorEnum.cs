namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// API error categories.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApiErrorEnum
    {
        /// <summary>
        /// Authentication failed.
        /// </summary>
        AuthenticationFailed,

        /// <summary>
        /// Resource not found.
        /// </summary>
        NotFound,

        /// <summary>
        /// Bad request.
        /// </summary>
        BadRequest,

        /// <summary>
        /// Conflict with existing resource.
        /// </summary>
        Conflict,

        /// <summary>
        /// Internal server error.
        /// </summary>
        InternalError,

        /// <summary>
        /// Action forbidden.
        /// </summary>
        Forbidden
    }
}
