namespace Tablix.Tests
{
    using System.Collections.Generic;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;
    using Xunit;

    /// <summary>
    /// Tests for value clamping and null guards across settings and model classes.
    /// </summary>
    public class SettingsClampingTests
    {
        /// <summary>
        /// RestSettings.Port clamps value of 0 up to 1.
        /// </summary>
        [Fact]
        public void RestSettings_Port_ZeroClampsToOne()
        {
            RestSettings rest = new RestSettings();
            rest.Port = 0;
            Assert.Equal(1, rest.Port);
        }

        /// <summary>
        /// RestSettings.Port clamps value of 99999 down to 65535.
        /// </summary>
        [Fact]
        public void RestSettings_Port_OverflowClampsToMax()
        {
            RestSettings rest = new RestSettings();
            rest.Port = 99999;
            Assert.Equal(65535, rest.Port);
        }

        /// <summary>
        /// RestSettings.Port preserves valid value of 9100.
        /// </summary>
        [Fact]
        public void RestSettings_Port_ValidValuePreserved()
        {
            RestSettings rest = new RestSettings();
            rest.Port = 9100;
            Assert.Equal(9100, rest.Port);
        }

        /// <summary>
        /// RestSettings.McpPort clamps similarly to Port.
        /// </summary>
        [Fact]
        public void RestSettings_McpPort_ClampsToValidRange()
        {
            RestSettings rest = new RestSettings();
            rest.McpPort = 0;
            Assert.Equal(1, rest.McpPort);

            rest.McpPort = 99999;
            Assert.Equal(65535, rest.McpPort);

            rest.McpPort = 8080;
            Assert.Equal(8080, rest.McpPort);
        }

        /// <summary>
        /// RestSettings.Hostname null defaults to "localhost".
        /// </summary>
        [Fact]
        public void RestSettings_Hostname_NullDefaultsToLocalhost()
        {
            RestSettings rest = new RestSettings();
            rest.Hostname = null;
            Assert.Equal("localhost", rest.Hostname);
        }

        /// <summary>
        /// LoggingSettings.MinimumSeverity clamps to 0-7.
        /// </summary>
        [Fact]
        public void LoggingSettings_MinimumSeverity_ClampsToRange()
        {
            LoggingSettings logging = new LoggingSettings();

            logging.MinimumSeverity = -1;
            Assert.Equal(0, logging.MinimumSeverity);

            logging.MinimumSeverity = 10;
            Assert.Equal(7, logging.MinimumSeverity);

            logging.MinimumSeverity = 3;
            Assert.Equal(3, logging.MinimumSeverity);
        }

        /// <summary>
        /// LoggingSettings.LogDirectory null defaults to "./logs/".
        /// </summary>
        [Fact]
        public void LoggingSettings_LogDirectory_NullDefaultsToLogsDir()
        {
            LoggingSettings logging = new LoggingSettings();
            logging.LogDirectory = null;
            Assert.Equal("./logs/", logging.LogDirectory);
        }

        /// <summary>
        /// DatabaseEntry.Port clamps to 1-65535 and supports null.
        /// </summary>
        [Fact]
        public void DatabaseEntry_Port_ClampsToValidRange()
        {
            DatabaseEntry entry = new DatabaseEntry();

            Assert.Null(entry.Port);

            entry.Port = 0;
            Assert.Equal(1, entry.Port);

            entry.Port = 99999;
            Assert.Equal(65535, entry.Port);

            entry.Port = 3306;
            Assert.Equal(3306, entry.Port);

            entry.Port = null;
            Assert.Null(entry.Port);
        }

        /// <summary>
        /// DatabaseEntry.AllowedQueries null defaults to empty list.
        /// </summary>
        [Fact]
        public void DatabaseEntry_AllowedQueries_NullDefaultsToEmptyList()
        {
            DatabaseEntry entry = new DatabaseEntry();
            entry.AllowedQueries = null;
            Assert.NotNull(entry.AllowedQueries);
            Assert.Empty(entry.AllowedQueries);
        }

        /// <summary>
        /// TablixSettings.Rest null assignment is ignored (stays as default, not null).
        /// </summary>
        [Fact]
        public void TablixSettings_Rest_NullAssignmentIgnored()
        {
            TablixSettings settings = new TablixSettings();
            settings.Rest = null;
            Assert.NotNull(settings.Rest);
        }

        /// <summary>
        /// TablixSettings.Databases null defaults to empty list.
        /// </summary>
        [Fact]
        public void TablixSettings_Databases_NullDefaultsToEmptyList()
        {
            TablixSettings settings = new TablixSettings();
            settings.Databases = null;
            Assert.NotNull(settings.Databases);
            Assert.Empty(settings.Databases);
        }

        /// <summary>
        /// TablixSettings.ApiKeys null defaults to empty list.
        /// </summary>
        [Fact]
        public void TablixSettings_ApiKeys_NullDefaultsToEmptyList()
        {
            TablixSettings settings = new TablixSettings();
            settings.ApiKeys = null;
            Assert.NotNull(settings.ApiKeys);
            Assert.Empty(settings.ApiKeys);
        }

        /// <summary>
        /// EnumerationQuery.MaxResults clamps to 1-1000.
        /// </summary>
        [Fact]
        public void EnumerationQuery_MaxResults_ClampsToRange()
        {
            EnumerationQuery query = new EnumerationQuery();

            query.MaxResults = 0;
            Assert.Equal(1, query.MaxResults);

            query.MaxResults = 5000;
            Assert.Equal(1000, query.MaxResults);

            query.MaxResults = 50;
            Assert.Equal(50, query.MaxResults);
        }

        /// <summary>
        /// EnumerationQuery.Skip clamps to >= 0.
        /// </summary>
        [Fact]
        public void EnumerationQuery_Skip_ClampsToNonNegative()
        {
            EnumerationQuery query = new EnumerationQuery();

            query.Skip = -5;
            Assert.Equal(0, query.Skip);

            query.Skip = 10;
            Assert.Equal(10, query.Skip);
        }
    }
}
