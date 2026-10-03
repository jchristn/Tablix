namespace Tablix.Server.Observability
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Observability;
    using Voltaic.Core;

    /// <summary>
    /// Telemetry for the MCP server. Voltaic's MCP HTTP transport is not Watson, so Tablix opens the server span and
    /// records the MCP metrics itself: one "tools/call {tool}" server span per tool call, parented on the caller's
    /// W3C traceparent when one is sent, plus the <see cref="TelemetryNames.MetricMcpToolCalls"/> family.
    /// Thread safety: all members are safe for concurrent use.
    /// </summary>
    public static class McpTelemetry
    {
        #region Public-Members

        /// <summary>
        /// Claim key that carries the inbound W3C traceparent from the authentication hook to the tool handler.
        /// </summary>
        public const string TraceParentClaim = "traceparent";

        /// <summary>
        /// Claim key that carries the inbound W3C tracestate from the authentication hook to the tool handler.
        /// </summary>
        public const string TraceStateClaim = "tracestate";

        #endregion

        #region Private-Members

        private static readonly ConcurrentDictionary<Type, PropertyInfo> _SuccessProperties = new ConcurrentDictionary<Type, PropertyInfo>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Voltaic authentication hook that always admits the request (Tablix MCP is unauthenticated) and copies the
        /// W3C trace context headers into the call context claims so the tool handler can continue the caller's trace.
        /// </summary>
        /// <param name="request">Inbound HTTP request.</param>
        /// <returns>An authenticated result with no principal.</returns>
        public static Task<AuthenticationResult> CaptureTraceContextAsync(HttpListenerRequest request)
        {
            AuthenticationResult result = new AuthenticationResult { IsAuthenticated = true };
            try
            {
                string traceparent = request?.Headers[TraceParentClaim];
                if (!String.IsNullOrWhiteSpace(traceparent))
                {
                    Dictionary<string, string> claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [TraceParentClaim] = traceparent
                    };

                    string tracestate = request.Headers[TraceStateClaim];
                    if (!String.IsNullOrWhiteSpace(tracestate)) claims[TraceStateClaim] = tracestate;
                    result.Claims = claims;
                }
            }
            catch (Exception)
            {
                // Trace context capture is best-effort; the request is still admitted.
            }

            return Task.FromResult(result);
        }

        /// <summary>
        /// Run one MCP tool handler inside a server span and record its metrics. The handler's result and exceptions
        /// pass through unchanged. A result whose public boolean Success property is false is recorded as a failure.
        /// </summary>
        /// <param name="toolName">Registered tool name.</param>
        /// <param name="callContext">Voltaic call context carrying the trace context claims. May be null.</param>
        /// <param name="handler">Tool handler.</param>
        /// <param name="token">Cancellation token for the MCP request.</param>
        /// <returns>The handler's result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the request was canceled before the tool ran.</exception>
        public static async Task<object> InvokeToolAsync(string toolName, RpcCallContext callContext, Func<Task<object>> handler, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(handler);
            token.ThrowIfCancellationRequested();

            ActivityContext parent = default;
            if (callContext?.Claims != null)
            {
                callContext.Claims.TryGetValue(TraceParentClaim, out string traceparent);
                callContext.Claims.TryGetValue(TraceStateClaim, out string tracestate);
                parent = TablixTracing.ParseTraceParent(traceparent, tracestate);
            }

            long start = Stopwatch.GetTimestamp();
            using Activity activity = TablixTracing.StartWithParent(TelemetryNames.SpanMcpToolCallPrefix + toolName, ActivityKind.Server, parent);
            TablixTracing.SetTag(activity, TelemetryNames.AttrMcpMethod, "tools/call");
            TablixTracing.SetTag(activity, TelemetryNames.AttrToolName, toolName);
            TablixTracing.SetTag(activity, TelemetryNames.AttrGenAiOperation, "execute_tool");
            TablixMetrics.AddMcpToolActive(1);

            string outcome = TelemetryNames.OutcomeSuccess;
            try
            {
                object result = await handler().ConfigureAwait(false);
                if (ReportsFailure(result))
                {
                    outcome = TelemetryNames.OutcomeFailure;
                    TablixTracing.SetOutcome(activity, outcome, "tool_failed", "The tool reported a failure result.");
                }
                else
                {
                    TablixTracing.SetSuccess(activity);
                }

                return result;
            }
            catch (Exception ex)
            {
                outcome = TablixMetrics.OutcomeOf(ex);
                TablixTracing.RecordException(activity, ex);
                TablixMetrics.RecordError(TelemetryNames.ComponentMcp, ex);
                throw;
            }
            finally
            {
                TablixMetrics.AddMcpToolActive(-1);
                TablixMetrics.RecordMcpToolCall(toolName, outcome, TablixMetrics.SecondsSince(start));
            }
        }

        #endregion

        #region Private-Methods

        private static bool ReportsFailure(object result)
        {
            if (result == null) return false;

            try
            {
                PropertyInfo property = _SuccessProperties.GetOrAdd(result.GetType(), type =>
                {
                    PropertyInfo candidate = type.GetProperty("Success", BindingFlags.Public | BindingFlags.Instance);
                    return candidate != null && candidate.PropertyType == typeof(bool) ? candidate : null;
                });

                return property != null && !(bool)property.GetValue(result);
            }
            catch (Exception)
            {
                return false;
            }
        }

        #endregion
    }
}
