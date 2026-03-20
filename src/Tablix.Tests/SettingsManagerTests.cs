namespace Tablix.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Tablix.Core.Settings;
    using Tablix.Server;
    using Xunit;

    /// <summary>
    /// Integration tests for <see cref="SettingsManager"/>.
    /// </summary>
    public class SettingsManagerTests : IDisposable
    {
        private readonly string _TempFile;

        /// <summary>
        /// Instantiate and create a unique temp file path.
        /// </summary>
        public SettingsManagerTests()
        {
            _TempFile = Path.Combine(Path.GetTempPath(), "tablix_settings_" + Guid.NewGuid().ToString("N") + ".json");
        }

        /// <summary>
        /// Clean up the temp settings file.
        /// </summary>
        public void Dispose()
        {
            if (File.Exists(_TempFile))
                File.Delete(_TempFile);
        }

        /// <summary>
        /// Creating a SettingsManager with a nonexistent file creates defaults.
        /// </summary>
        [Fact]
        public void Constructor_CreatesDefaultSettings()
        {
            SettingsManager manager = new SettingsManager(_TempFile);
            Assert.NotNull(manager.Settings);
            Assert.True(File.Exists(_TempFile));
        }

        /// <summary>
        /// AddDatabase then GetDatabase returns the added entry.
        /// </summary>
        [Fact]
        public void AddDatabase_ThenGetDatabase_ReturnsEntry()
        {
            SettingsManager manager = new SettingsManager(_TempFile);

            DatabaseEntry entry = new DatabaseEntry { Id = "test_db_1" };
            manager.AddDatabase(entry);

            DatabaseEntry retrieved = manager.GetDatabase("test_db_1");
            Assert.NotNull(retrieved);
            Assert.Equal("test_db_1", retrieved.Id);
        }

        /// <summary>
        /// UpdateDatabase modifies an existing entry.
        /// </summary>
        [Fact]
        public void UpdateDatabase_ModifiesEntry()
        {
            SettingsManager manager = new SettingsManager(_TempFile);

            DatabaseEntry entry = new DatabaseEntry { Id = "update_db" };
            entry.DatabaseName = "OriginalName";
            manager.AddDatabase(entry);

            DatabaseEntry updated = new DatabaseEntry { Id = "update_db" };
            updated.DatabaseName = "UpdatedName";
            manager.UpdateDatabase(updated);

            DatabaseEntry retrieved = manager.GetDatabase("update_db");
            Assert.NotNull(retrieved);
            Assert.Equal("UpdatedName", retrieved.DatabaseName);
        }

        /// <summary>
        /// DeleteDatabase removes the entry.
        /// </summary>
        [Fact]
        public void DeleteDatabase_RemovesEntry()
        {
            SettingsManager manager = new SettingsManager(_TempFile);

            DatabaseEntry entry = new DatabaseEntry { Id = "delete_db" };
            manager.AddDatabase(entry);

            manager.DeleteDatabase("delete_db");

            DatabaseEntry retrieved = manager.GetDatabase("delete_db");
            Assert.Null(retrieved);
        }

        /// <summary>
        /// AddDatabase with a duplicate ID throws InvalidOperationException.
        /// </summary>
        [Fact]
        public void AddDatabase_DuplicateId_ThrowsInvalidOperationException()
        {
            SettingsManager manager = new SettingsManager(_TempFile);

            DatabaseEntry entry1 = new DatabaseEntry { Id = "dup_db" };
            manager.AddDatabase(entry1);

            DatabaseEntry entry2 = new DatabaseEntry { Id = "dup_db" };
            Assert.Throws<InvalidOperationException>(() => manager.AddDatabase(entry2));
        }

        /// <summary>
        /// DeleteDatabase with an unknown ID throws KeyNotFoundException.
        /// </summary>
        [Fact]
        public void DeleteDatabase_UnknownId_ThrowsKeyNotFoundException()
        {
            SettingsManager manager = new SettingsManager(_TempFile);
            Assert.Throws<KeyNotFoundException>(() => manager.DeleteDatabase("nonexistent_db"));
        }
    }
}
