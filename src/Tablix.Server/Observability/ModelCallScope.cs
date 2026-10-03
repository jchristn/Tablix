namespace Tablix.Server.Observability
{
    using System;
    using System.Diagnostics;
    using PolyPrompt.Models;
    using Tablix.Core.Observability;
    using Tablix.Core.Settings;

    /// <summary>
    /// Instruments one outbound model provider call: a "{provider} {operation}" client span with OpenTelemetry gen_ai
    /// attributes, plus the <see cref="TelemetryNames.MetricModelRequests"/>, duration, token, and time-to-first-token
    /// metrics. Prompts, completions, and API keys are never recorded. Complete the scope with one of the Complete
    /// methods; disposing an uncompleted scope records an error (the call threw).
    /// Thread safety: not thread-safe; one scope belongs to one call.
    /// </summary>
    public sealed class ModelCallScope : IDisposable
    {
        #region Private-Members

        private readonly Activity _Activity;
        private readonly string _Provider;
        private readonly string _Operation;
        private readonly long _Start;
        private readonly string _ApiKey;
        private bool _Completed = false;

        #endregion

        #region Constructors-and-Factories

        private ModelCallScope(ModelProviderSettings provider, string operation)
        {
            _Provider = provider == null ? TelemetryNames.ValueUnknown : TablixMetrics.GenAiProvider(provider.Type);
            _Operation = operation;
            _Start = Stopwatch.GetTimestamp();
            _ApiKey = provider?.ApiKey;
            _Activity = TablixTracing.Start(_Provider + " " + operation, ActivityKind.Client);
            TablixTracing.SetTag(_Activity, TelemetryNames.AttrGenAiProvider, _Provider);
            TablixTracing.SetTag(_Activity, TelemetryNames.AttrGenAiOperation, operation);
            if (provider != null)
            {
                TablixTracing.SetTag(_Activity, TelemetryNames.AttrProviderId, provider.Id);
                TablixTracing.SetTag(_Activity, TelemetryNames.AttrGenAiRequestModel, provider.Model);
                if (Uri.TryCreate(provider.Endpoint, UriKind.Absolute, out Uri endpoint))
                {
                    TablixTracing.SetTag(_Activity, TelemetryNames.AttrServerAddress, endpoint.Host);
                    TablixTracing.SetTag(_Activity, TelemetryNames.AttrServerPort, endpoint.Port);
                }
            }
        }

        /// <summary>
        /// Start instrumenting a model provider call.
        /// </summary>
        /// <param name="provider">Provider settings. May be null (labels become unknown).</param>
        /// <param name="operation">Operation value from <see cref="TelemetryNames"/> (chat, chat_stream, ...).</param>
        /// <returns>The scope.</returns>
        public static ModelCallScope Start(ModelProviderSettings provider, string operation)
        {
            return new ModelCallScope(provider, operation);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Complete the scope from a provider response.
        /// </summary>
        /// <param name="success">Whether the provider reported success.</param>
        /// <param name="responseModel">Model reported by the provider. May be null.</param>
        /// <param name="statusCode">HTTP status code reported by the provider. May be null.</param>
        /// <param name="usage">Token usage reported by the provider. May be null.</param>
        /// <param name="timeToFirstTokenMs">Time to first token for streamed calls, or 0.</param>
        /// <param name="error">Provider error message for failures. Recorded as the span status description with the API
        /// key redacted and truncated to 256 characters.</param>
        public void Complete(bool success, string responseModel, int? statusCode, ChatStreamingUsage usage, long timeToFirstTokenMs, string error)
        {
            if (_Completed) return;
            _Completed = true;

            try
            {
                if (!String.IsNullOrWhiteSpace(responseModel)) TablixTracing.SetTag(_Activity, TelemetryNames.AttrGenAiResponseModel, responseModel);
                if (statusCode.HasValue && statusCode.Value > 0) TablixTracing.SetTag(_Activity, "http.response.status_code", statusCode.Value);

                long inputTokens = usage?.PromptTokens ?? 0;
                long outputTokens = usage?.CompletionTokens ?? 0;
                if (inputTokens > 0) TablixTracing.SetTag(_Activity, TelemetryNames.AttrGenAiInputTokens, inputTokens);
                if (outputTokens > 0) TablixTracing.SetTag(_Activity, TelemetryNames.AttrGenAiOutputTokens, outputTokens);
                TablixMetrics.RecordModelTokens(_Provider, inputTokens, outputTokens);

                if (timeToFirstTokenMs > 0) TablixMetrics.RecordTimeToFirstToken(_Provider, timeToFirstTokenMs / 1000.0);

                string outcome = success ? TelemetryNames.OutcomeSuccess : TelemetryNames.OutcomeFailure;
                if (success) TablixTracing.SetSuccess(_Activity);
                else TablixTracing.SetOutcome(_Activity, outcome, statusCode.HasValue && statusCode.Value > 0 ? statusCode.Value.ToString() : "provider_error", Redact(error));

                TablixMetrics.RecordModelRequest(_Provider, _Operation, outcome, TablixMetrics.SecondsSince(_Start));
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Complete the scope for a call that threw.
        /// </summary>
        /// <param name="ex">Exception.</param>
        public void Complete(Exception ex)
        {
            if (_Completed) return;
            _Completed = true;

            TablixTracing.RecordException(_Activity, ex);
            TablixMetrics.RecordModelRequest(_Provider, _Operation, TablixMetrics.OutcomeOf(ex), TablixMetrics.SecondsSince(_Start));
        }

        /// <summary>
        /// End the span. An uncompleted scope is recorded as an error.
        /// </summary>
        public void Dispose()
        {
            if (!_Completed)
            {
                _Completed = true;
                TablixTracing.SetOutcome(_Activity, TelemetryNames.OutcomeError, "incomplete");
                TablixMetrics.RecordModelRequest(_Provider, _Operation, TelemetryNames.OutcomeError, TablixMetrics.SecondsSince(_Start));
            }

            try { _Activity?.Dispose(); } catch (Exception) { }
        }

        #endregion

        #region Private-Methods

        private string Redact(string message)
        {
            if (String.IsNullOrEmpty(message)) return null;

            string redacted = message;
            if (!String.IsNullOrEmpty(_ApiKey)) redacted = redacted.Replace(_ApiKey, "[redacted]", StringComparison.Ordinal);
            return redacted.Length > 256 ? redacted.Substring(0, 256) : redacted;
        }

        #endregion
    }
}
