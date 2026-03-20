namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Standardized API error response.
    /// </summary>
    public class ApiErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// Error category.
        /// </summary>
        public ApiErrorEnum Error { get; set; } = ApiErrorEnum.InternalError;

        /// <summary>
        /// Human-readable error message.
        /// </summary>
        public string Message
        {
            get
            {
                return Error switch
                {
                    ApiErrorEnum.AuthenticationFailed => "Authentication failed. Please check your credentials.",
                    ApiErrorEnum.NotFound => "The requested resource was not found.",
                    ApiErrorEnum.BadRequest => "The request was malformed or invalid.",
                    ApiErrorEnum.Conflict => "A conflict occurred with an existing resource.",
                    ApiErrorEnum.Forbidden => "This action is not permitted.",
                    _ => "An internal server error occurred."
                };
            }
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode
        {
            get
            {
                return Error switch
                {
                    ApiErrorEnum.AuthenticationFailed => 401,
                    ApiErrorEnum.NotFound => 404,
                    ApiErrorEnum.BadRequest => 400,
                    ApiErrorEnum.Conflict => 409,
                    ApiErrorEnum.Forbidden => 403,
                    _ => 500
                };
            }
        }

        /// <summary>
        /// Optional additional detail.
        /// </summary>
        public string Description { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ApiErrorResponse()
        {
        }

        /// <summary>
        /// Instantiate with error type.
        /// </summary>
        /// <param name="error">Error category.</param>
        /// <param name="description">Optional description.</param>
        public ApiErrorResponse(ApiErrorEnum error, string description = null)
        {
            Error = error;
            Description = description;
        }

        #endregion
    }
}
