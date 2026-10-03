namespace Tablix.Server.Observability
{
    /// <summary>
    /// Mutable outcome holder for one instrumented request (chat or context build). The request body sets these
    /// values as it learns them; the instrumentation wrapper records them when the request ends.
    /// Thread safety: not thread-safe; one instance belongs to one request.
    /// </summary>
    public class RequestTelemetryState
    {
        #region Public-Members

        /// <summary>
        /// Outcome set explicitly by the request body (for example failure for a streamed chat whose model call failed
        /// after a 200 response was already sent). Null means: derive the outcome from the HTTP response. Default null.
        /// </summary>
        public string Outcome { get; set; } = null;

        /// <summary>
        /// Bounded error type for a non-success outcome. Default null.
        /// </summary>
        public string ErrorType { get; set; } = null;

        /// <summary>
        /// Chat execution path chosen by the server (bounded set). Default null (reported as unknown).
        /// </summary>
        public string ExecutionPath { get; set; } = null;

        #endregion
    }
}
