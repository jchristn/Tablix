namespace Tablix.Core.Observability
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Tablix application tracing. Every span starts on the <see cref="TelemetryNames.ActivitySourceName"/> activity
    /// source. With no listener attached, <see cref="ActivitySource.StartActivity(string, ActivityKind)"/> returns null
    /// and every helper below is a no-op; every helper is best-effort and never throws.
    /// Thread safety: all members are safe for concurrent use.
    /// </summary>
    public static class TablixTracing
    {
        #region Public-Members

        /// <summary>
        /// The Tablix activity source. Subscribe to <see cref="TelemetryNames.ActivitySourceName"/> to collect it.
        /// </summary>
        public static ActivitySource Source
        {
            get { return _Source; }
        }

        #endregion

        #region Private-Members

        private static readonly ActivitySource _Source = new ActivitySource(TelemetryNames.ActivitySourceName, Helpers.Constants.ProductVersion);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start a span as a child of <see cref="Activity.Current"/> (or as a root when there is none). The new span
        /// becomes <see cref="Activity.Current"/> until it is disposed.
        /// </summary>
        /// <param name="name">Span name. Must be low-cardinality (see <see cref="TelemetryNames"/>).</param>
        /// <param name="kind">Span kind. Default internal.</param>
        /// <returns>The span, or null when nothing is listening or creation failed.</returns>
        public static Activity Start(string name, ActivityKind kind = ActivityKind.Internal)
        {
            try
            {
                return _Source.StartActivity(name, kind);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Start a span with an explicit parent context. A default parent context starts a new root trace even when
        /// <see cref="Activity.Current"/> is set. The new span becomes <see cref="Activity.Current"/> until disposed.
        /// Use this at background hand-offs and inbound boundaries so unrelated work does not join the caller's trace.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="kind">Span kind.</param>
        /// <param name="parent">Parent context, or default for a new root.</param>
        /// <param name="link">Optional context to link (for example the trace that scheduled this work).</param>
        /// <returns>The span, or null when nothing is listening or creation failed.</returns>
        public static Activity StartWithParent(string name, ActivityKind kind, ActivityContext parent, ActivityContext link = default)
        {
            try
            {
                ActivityLink[] links = link == default ? null : new[] { new ActivityLink(link) };
                if (parent == default)
                {
                    // StartActivity falls back to Activity.Current for a default parent, so clear it to force a root.
                    Activity.Current = null;
                }

                return _Source.StartActivity(name, kind, parent, null, links);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Start a child span of <paramref name="parent"/> without changing <see cref="Activity.Current"/>. Use this for
        /// stage spans opened and closed from callbacks, where changing the ambient span would leak into other work.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="parent">Parent span. When null, no span is started.</param>
        /// <param name="kind">Span kind. Default internal.</param>
        /// <returns>The span, or null when there is no parent, nothing is listening, or creation failed.</returns>
        public static Activity StartDetachedChild(string name, Activity parent, ActivityKind kind = ActivityKind.Internal)
        {
            if (parent == null) return null;

            Activity previous = Activity.Current;
            try
            {
                return _Source.StartActivity(name, kind, parent.Context);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Activity.Current = previous;
            }
        }

        /// <summary>
        /// Start a child span of <paramref name="parent"/> with an explicit, earlier start time, without changing
        /// <see cref="Activity.Current"/>. Use this to record a stage that is only known to have happened once it ends,
        /// such as time spent queued.
        /// </summary>
        /// <param name="name">Span name.</param>
        /// <param name="parent">Parent span. When null, no span is started.</param>
        /// <param name="startTime">Start time of the span.</param>
        /// <returns>The span, or null when there is no parent, nothing is listening, or creation failed.</returns>
        public static Activity StartDetachedChild(string name, Activity parent, DateTimeOffset startTime)
        {
            if (parent == null) return null;

            Activity previous = Activity.Current;
            try
            {
                return _Source.StartActivity(name, ActivityKind.Internal, parent.Context, null, null, startTime);
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Activity.Current = previous;
            }
        }

        /// <summary>
        /// Stop a span started with <see cref="StartDetachedChild(string, Activity, ActivityKind)"/> without changing <see cref="Activity.Current"/>.
        /// </summary>
        /// <param name="activity">Span. May be null.</param>
        public static void StopDetached(Activity activity)
        {
            if (activity == null) return;

            Activity previous = Activity.Current;
            try
            {
                activity.Stop();
            }
            catch (Exception)
            {
            }
            finally
            {
                Activity.Current = previous;
            }
        }

        /// <summary>
        /// Name a stage span ("stage:{name}").
        /// </summary>
        /// <param name="stage">Stage name.</param>
        /// <returns>Span name.</returns>
        public static string StageSpanName(string stage)
        {
            return TelemetryNames.SpanStagePrefix + stage;
        }

        /// <summary>
        /// Set a span attribute. No-op for a null span.
        /// </summary>
        /// <param name="activity">Span. May be null.</param>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value. Never pass secrets, credentials, prompts, or result payloads.</param>
        public static void SetTag(Activity activity, string key, object value)
        {
            if (activity == null) return;
            try { activity.SetTag(key, value); } catch (Exception) { }
        }

        /// <summary>
        /// Mark a span successful and tag its outcome. No-op for a null span.
        /// </summary>
        /// <param name="activity">Span. May be null.</param>
        public static void SetSuccess(Activity activity)
        {
            if (activity == null) return;
            try
            {
                activity.SetTag(TelemetryNames.AttrOutcome, TelemetryNames.OutcomeSuccess);
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Mark a span with a non-success outcome. Failure, error, and canceled set the span status to error; rejected
        /// is a policy decision and leaves the status unset. No-op for a null span.
        /// </summary>
        /// <param name="activity">Span. May be null.</param>
        /// <param name="outcome">Outcome value.</param>
        /// <param name="errorType">Bounded error type (exception type or fixed code).</param>
        /// <param name="description">Optional human description. Must not contain secrets or payloads.</param>
        public static void SetOutcome(Activity activity, string outcome, string errorType, string description = null)
        {
            if (activity == null) return;
            try
            {
                activity.SetTag(TelemetryNames.AttrOutcome, outcome);
                if (outcome == TelemetryNames.OutcomeSuccess)
                {
                    activity.SetStatus(ActivityStatusCode.Ok);
                    return;
                }

                if (!String.IsNullOrEmpty(errorType)) activity.SetTag(TelemetryNames.AttrErrorType, errorType);
                if (outcome != TelemetryNames.OutcomeRejected) activity.SetStatus(ActivityStatusCode.Error, description);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Record an exception on a span: adds the OpenTelemetry exception event, sets error.type, and sets the
        /// status to error (canceled operations are tagged canceled). No-op for a null span.
        /// </summary>
        /// <param name="activity">Span. May be null.</param>
        /// <param name="ex">Exception. May be null.</param>
        public static void RecordException(Activity activity, Exception ex)
        {
            if (activity == null || ex == null) return;
            try
            {
                activity.AddException(ex);
                SetOutcome(activity, TablixMetrics.OutcomeOf(ex), ex.GetType().Name, ex.Message);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Parse a W3C traceparent (and optional tracestate) into a parent context.
        /// </summary>
        /// <param name="traceparent">W3C traceparent header value. May be null.</param>
        /// <param name="tracestate">W3C tracestate header value. May be null.</param>
        /// <returns>The parsed context, or default when absent or invalid.</returns>
        public static ActivityContext ParseTraceParent(string traceparent, string tracestate)
        {
            if (String.IsNullOrWhiteSpace(traceparent)) return default;

            try
            {
                return ActivityContext.TryParse(traceparent, tracestate, true, out ActivityContext context) ? context : default;
            }
            catch (Exception)
            {
                return default;
            }
        }

        #endregion
    }
}
