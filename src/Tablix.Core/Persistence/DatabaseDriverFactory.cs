namespace Tablix.Core.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Enums;
    using Tablix.Core.Persistence.Sqlite;
    using Tablix.Core.Settings;

    /// <summary>
    /// Factory for product-state persistence drivers.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        /// <summary>
        /// Create a persistence driver.
        /// </summary>
        /// <param name="settings">Persistence database settings.</param>
        /// <returns>Persistence driver.</returns>
        public static DatabaseDriverBase Create(PersistenceDatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            if (settings.Type == TablixPersistenceDatabaseTypeEnum.Sqlite)
                return new SqliteDatabaseDriver(settings.Filename);

            throw new NotSupportedException("Persistence database type '" + settings.Type + "' is not supported.");
        }

        /// <summary>
        /// Create and initialize a persistence driver.
        /// </summary>
        /// <param name="settings">Persistence database settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Initialized persistence driver.</returns>
        public static async Task<DatabaseDriverBase> CreateAndInitializeAsync(PersistenceDatabaseSettings settings, CancellationToken token = default)
        {
            DatabaseDriverBase driver = Create(settings);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            return driver;
        }
    }
}
