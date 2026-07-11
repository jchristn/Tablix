namespace Tablix.Core.Helpers
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// JSON serialization helper.
    /// </summary>
    public static class Serializer
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private static readonly JsonSerializerOptions _Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Serialize an object to JSON.
        /// </summary>
        /// <param name="obj">Object to serialize.</param>
        /// <param name="pretty">Pretty print.</param>
        /// <returns>JSON string.</returns>
        public static string SerializeJson(object obj, bool pretty = true)
        {
            if (obj == null) return null;

            if (pretty)
            {
                return JsonSerializer.Serialize(obj, _Options);
            }
            else
            {
                JsonSerializerOptions compact = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = null,
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = false,
                    Converters = { new JsonStringEnumConverter() }
                };

                return JsonSerializer.Serialize(obj, compact);
            }
        }

        /// <summary>
        /// Deserialize JSON to a typed object.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="json">JSON string.</param>
        /// <returns>Deserialized object.</returns>
        public static T DeserializeJson<T>(string json)
        {
            if (String.IsNullOrEmpty(json)) throw new ArgumentNullException(nameof(json));
            return JsonSerializer.Deserialize<T>(json, _Options);
        }

        /// <summary>
        /// Deserialize an object by first converting it through JSON.
        /// </summary>
        /// <typeparam name="T">Target type.</typeparam>
        /// <param name="obj">Source object.</param>
        /// <returns>Deserialized object.</returns>
        public static T DeserializeObject<T>(object obj)
        {
            if (obj == null) return default(T);

            string json = JsonSerializer.Serialize(obj, _Options);
            return DeserializeJson<T>(json);
        }

        #endregion
    }
}
