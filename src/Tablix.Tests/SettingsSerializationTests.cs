namespace Tablix.Tests
{
    using System.Collections.Generic;
    using Tablix.Core.Helpers;
    using Tablix.Core.Settings;
    using Xunit;

    /// <summary>
    /// Tests for settings round-trip serialization via <see cref="Serializer"/>.
    /// </summary>
    public class SettingsSerializationTests
    {
        /// <summary>
        /// Serialize default TablixSettings to JSON, deserialize back, and verify all fields match.
        /// </summary>
        [Fact]
        public void RoundTrip_DefaultSettings_AllFieldsPreserved()
        {
            TablixSettings original = new TablixSettings();

            string json = Serializer.SerializeJson(original);
            TablixSettings restored = Serializer.DeserializeJson<TablixSettings>(json);

            Assert.NotNull(restored);
            Assert.NotNull(restored.Rest);
            Assert.Equal(original.Rest.Hostname, restored.Rest.Hostname);
            Assert.Equal(original.Rest.Port, restored.Rest.Port);
            Assert.Equal(original.Rest.Ssl, restored.Rest.Ssl);
            Assert.Equal(original.Rest.McpPort, restored.Rest.McpPort);

            Assert.NotNull(restored.Logging);
            Assert.Equal(original.Logging.ConsoleLogging, restored.Logging.ConsoleLogging);
            Assert.Equal(original.Logging.FileLogging, restored.Logging.FileLogging);
            Assert.Equal(original.Logging.LogDirectory, restored.Logging.LogDirectory);
            Assert.Equal(original.Logging.LogFilename, restored.Logging.LogFilename);
            Assert.Equal(original.Logging.MinimumSeverity, restored.Logging.MinimumSeverity);
            Assert.Equal(original.Logging.EnableColors, restored.Logging.EnableColors);

            Assert.NotNull(restored.Databases);
            Assert.Equal(original.Databases.Count, restored.Databases.Count);

            Assert.NotNull(restored.ApiKeys);
            Assert.Equal(original.ApiKeys.Count, restored.ApiKeys.Count);
        }

        /// <summary>
        /// Verify JSON uses PascalCase property names.
        /// </summary>
        [Fact]
        public void Serialize_UsesPascalCasePropertyNames()
        {
            TablixSettings settings = new TablixSettings();

            string json = Serializer.SerializeJson(settings);

            Assert.Contains("Rest", json);
            Assert.Contains("Logging", json);
            Assert.Contains("Databases", json);
            Assert.Contains("ApiKeys", json);
        }

        /// <summary>
        /// Verify null properties are omitted from JSON output.
        /// </summary>
        [Fact]
        public void Serialize_NullProperties_OmittedFromJson()
        {
            DatabaseEntry entry = new DatabaseEntry();
            entry.Hostname = null;
            entry.User = null;
            entry.Password = null;
            entry.DatabaseName = null;
            entry.Filename = null;
            entry.Context = null;

            string json = Serializer.SerializeJson(entry);

            Assert.DoesNotContain("\"Hostname\"", json);
            Assert.DoesNotContain("\"User\"", json);
            Assert.DoesNotContain("\"Password\"", json);
            Assert.DoesNotContain("\"DatabaseName\"", json);
            Assert.DoesNotContain("\"Filename\"", json);
            Assert.DoesNotContain("\"Context\"", json);
        }
    }
}
