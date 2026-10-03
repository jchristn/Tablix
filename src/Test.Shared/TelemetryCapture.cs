namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Linq;

    /// <summary>
    /// In-memory telemetry listener for tests. Subscribes to the named meters and activity sources through the base
    /// class library (no exporter or collector), records every measurement and every stopped span, and detaches on
    /// dispose.
    /// </summary>
    public sealed class TelemetryCapture : IDisposable
    {
        #region Private-Members

        private readonly HashSet<string> _Sources;
        private readonly MeterListener _MeterListener = new MeterListener();
        private readonly ActivityListener _ActivityListener;
        private readonly ConcurrentQueue<CapturedMeasurement> _Measurements = new ConcurrentQueue<CapturedMeasurement>();
        private readonly ConcurrentQueue<Activity> _Activities = new ConcurrentQueue<Activity>();
        private readonly ConcurrentDictionary<string, Instrument> _Instruments = new ConcurrentDictionary<string, Instrument>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start capturing.
        /// </summary>
        /// <param name="sources">Meter and activity source names to capture (for example "Tablix", "Watson").</param>
        public TelemetryCapture(params string[] sources)
        {
            _Sources = new HashSet<string>(sources ?? Array.Empty<string>(), StringComparer.Ordinal);

            _MeterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (!_Sources.Contains(instrument.Meter.Name)) return;
                _Instruments[instrument.Meter.Name + "/" + instrument.Name] = instrument;
                listener.EnableMeasurementEvents(instrument);
            };
            _MeterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<int>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) => Add(instrument, value, tags));
            _MeterListener.Start();

            _ActivityListener = new ActivityListener
            {
                ShouldListenTo = source => _Sources.Contains(source.Name),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _Activities.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(_ActivityListener);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Instruments published by the captured meters, keyed by "meter/instrument".
        /// </summary>
        /// <returns>Snapshot of published instruments.</returns>
        public Dictionary<string, Instrument> PublishedInstruments()
        {
            return new Dictionary<string, Instrument>(_Instruments, StringComparer.Ordinal);
        }

        /// <summary>
        /// Pull observable instruments (gauges) so their current values are captured.
        /// </summary>
        public void CollectObservables()
        {
            _MeterListener.RecordObservableInstruments();
        }

        /// <summary>
        /// Captured measurements for one instrument that match every "key=value" filter.
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="filters">Tag filters.</param>
        /// <returns>Matching measurements.</returns>
        public List<CapturedMeasurement> Find(string instrument, params string[] filters)
        {
            return _Measurements.Where(m => m.Instrument == instrument && m.Matches(filters)).ToList();
        }

        /// <summary>
        /// Sum of captured values for one instrument that match every filter.
        /// </summary>
        /// <param name="instrument">Instrument name.</param>
        /// <param name="filters">Tag filters.</param>
        /// <returns>Sum of matching values.</returns>
        public double Sum(string instrument, params string[] filters)
        {
            return Find(instrument, filters).Sum(m => m.Value);
        }

        /// <summary>
        /// Stopped spans with the given name.
        /// </summary>
        /// <param name="name">Span display name.</param>
        /// <returns>Matching spans.</returns>
        public List<Activity> Spans(string name)
        {
            return _Activities.Where(a => a.DisplayName == name).ToList();
        }

        /// <summary>
        /// All stopped spans.
        /// </summary>
        /// <returns>Snapshot of stopped spans.</returns>
        public List<Activity> AllSpans()
        {
            return _Activities.ToList();
        }

        /// <summary>
        /// Stopped child spans of a parent span.
        /// </summary>
        /// <param name="parent">Parent span.</param>
        /// <returns>Child spans.</returns>
        public List<Activity> Children(Activity parent)
        {
            if (parent == null) return new List<Activity>();
            return _Activities.Where(a => a.ParentSpanId == parent.SpanId && a.TraceId == parent.TraceId).ToList();
        }

        /// <summary>
        /// Stop capturing.
        /// </summary>
        public void Dispose()
        {
            _MeterListener.Dispose();
            _ActivityListener.Dispose();
        }

        #endregion

        #region Private-Methods

        private void Add(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            CapturedMeasurement measurement = new CapturedMeasurement
            {
                Instrument = instrument.Name,
                Meter = instrument.Meter.Name,
                Value = value
            };

            foreach (KeyValuePair<string, object> tag in tags)
            {
                measurement.Tags[tag.Key] = tag.Value == null ? null : Convert.ToString(tag.Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            _Measurements.Enqueue(measurement);
        }

        #endregion
    }
}
