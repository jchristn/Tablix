namespace Tablix.Server
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Helpers;
    using Tablix.Core.Persistence;
    using Tablix.Core.Settings;

    /// <summary>
    /// Initializes Tablix persistence and imports legacy JSON product state when needed.
    /// </summary>
    public static class PersistenceBootstrapper
    {
        /// <summary>
        /// Create, initialize, and seed the persistence driver.
        /// </summary>
        /// <param name="settingsManager">Settings manager.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Initialized persistence driver.</returns>
        public static async Task<DatabaseDriverBase> InitializeAsync(SettingsManager settingsManager, CancellationToken token = default)
        {
            if (settingsManager == null) throw new ArgumentNullException(nameof(settingsManager));

            PersistenceDatabaseSettings persistenceSettings = new PersistenceDatabaseSettings
            {
                Type = settingsManager.Settings.Persistence.Type,
                Filename = ResolvePersistenceFilename(settingsManager.Filename, settingsManager.Settings.Persistence.Filename)
            };

            DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(persistenceSettings, token).ConfigureAwait(false);
            await ImportLegacyJsonAsync(driver, settingsManager.Filename, token).ConfigureAwait(false);
            return driver;
        }

        /// <summary>
        /// Resolve persistence database filename relative to the settings file.
        /// </summary>
        /// <param name="settingsFilename">Settings filename.</param>
        /// <param name="persistenceFilename">Persistence filename.</param>
        /// <returns>Resolved filename.</returns>
        public static string ResolvePersistenceFilename(string settingsFilename, string persistenceFilename)
        {
            if (string.IsNullOrWhiteSpace(persistenceFilename)) return "tablix.db";
            if (Path.IsPathRooted(persistenceFilename)) return persistenceFilename;

            string settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(settingsFilename ?? Constants.SettingsFilename));
            if (string.IsNullOrWhiteSpace(settingsDirectory)) settingsDirectory = Directory.GetCurrentDirectory();
            return Path.Combine(settingsDirectory, persistenceFilename);
        }

        private static async Task ImportLegacyJsonAsync(DatabaseDriverBase driver, string settingsFilename, CancellationToken token)
        {
            if (!File.Exists(settingsFilename)) return;

            long databaseCount = await driver.DatabaseConnections.CountAsync(null, token).ConfigureAwait(false);
            long providerCount = await driver.ModelProviders.CountAsync(null, null, token).ConfigureAwait(false);
            if (databaseCount > 0 && providerCount > 0) return;

            string json = File.ReadAllText(settingsFilename);
            LegacyTablixSettings legacy = Serializer.DeserializeJson<LegacyTablixSettings>(json);
            if (legacy == null) return;

            if (databaseCount == 0)
            {
                foreach (DatabaseEntry database in legacy.Databases)
                {
                    if (database == null || string.IsNullOrWhiteSpace(database.Id)) continue;
                    await driver.DatabaseConnections.CreateAsync(database, token).ConfigureAwait(false);
                }
            }

            if (providerCount == 0)
            {
                foreach (ModelProviderSettings provider in legacy.Chat.Providers)
                {
                    if (provider == null || string.IsNullOrWhiteSpace(provider.Id)) continue;
                    await driver.ModelProviders.CreateAsync(provider, token).ConfigureAwait(false);
                }
            }
        }
    }
}
