namespace Test.Shared
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One metric measurement captured by <see cref="TelemetryCapture"/>.
    /// </summary>
    public class CapturedMeasurement
    {
        #region Public-Members

        /// <summary>
        /// Instrument name.
        /// </summary>
        public string Instrument { get; set; } = null;

        /// <summary>
        /// Meter name.
        /// </summary>
        public string Meter { get; set; } = null;

        /// <summary>
        /// Measured value.
        /// </summary>
        public double Value { get; set; } = 0;

        /// <summary>
        /// Measurement tags, stringified.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Whether every "key=value" filter matches this measurement's tags.
        /// </summary>
        /// <param name="filters">Filters in "key=value" form.</param>
        /// <returns>True when all filters match.</returns>
        public bool Matches(params string[] filters)
        {
            if (filters == null) return true;

            foreach (string filter in filters)
            {
                int separator = filter.IndexOf('=');
                string key = separator < 0 ? filter : filter.Substring(0, separator);
                string value = separator < 0 ? null : filter.Substring(separator + 1);
                if (!Tags.TryGetValue(key, out string actual)) return false;
                if (value != null && !String.Equals(actual, value, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        #endregion
    }
}
