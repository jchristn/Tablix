namespace Tablix.Core.Persistence.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.Enums;
    using Tablix.Core.Persistence.Sqlite.Implementations;

    /// <summary>
    /// SQLite implementation of the Tablix product-state persistence driver.
    /// Thread safety: all persistence operations are serialized by a semaphore to protect the single SQLite state file.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        /// <summary>
        /// Persistence database type.
        /// </summary>
        public override TablixPersistenceDatabaseTypeEnum DatabaseType
        {
            get { return TablixPersistenceDatabaseTypeEnum.Sqlite; }
        }

        /// <summary>
        /// SQLite database filename.
        /// </summary>
        public string Filename { get; private set; } = null;

        private readonly SemaphoreSlim _OperationSemaphore = new SemaphoreSlim(1, 1);
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="filename">SQLite database filename.</param>
        public SqliteDatabaseDriver(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) throw new ArgumentNullException(nameof(filename));

            Filename = filename;
            ModelProviders = new SqliteModelProviderMethods(this);
            DatabaseConnections = new SqliteDatabaseConnectionMethods(this);
            DatabaseMetadata = new SqliteDatabaseMetadataMethods(this);
            DatabaseContexts = new SqliteDatabaseContextMethods(this);
            TableContexts = new SqliteTableContextMethods(this);
            SetupState = new SqliteSetupStateMethods(this);
        }

        /// <summary>
        /// Initialize the SQLite persistence database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public override async Task InitializeAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            string directory = Path.GetDirectoryName(Path.GetFullPath(Filename));
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using SqliteConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false);
            await ApplyMigrationsAsync(connection, token).ConfigureAwait(false);
            await SeedSetupStateAsync(connection, token).ConfigureAwait(false);
            await SeedDefaultsAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Close the SQLite persistence database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public override Task CloseAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        internal async Task<SqliteConnection> OpenConnectionAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder
            {
                DataSource = Filename,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default,
                Pooling = false
            };

            SqliteConnection connection = new SqliteConnection(builder.ConnectionString);
            await connection.OpenAsync(token).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys = ON", token).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA journal_mode = DELETE", token).ConfigureAwait(false);
            await ExecutePragmaAsync(connection, "PRAGMA busy_timeout = 5000", token).ConfigureAwait(false);
            return connection;
        }

        internal async Task<T> ExecuteReadAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken token)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            await _OperationSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using SqliteConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false);
                return await action(connection).ConfigureAwait(false);
            }
            finally
            {
                _OperationSemaphore.Release();
            }
        }

        internal async Task ExecuteWriteAsync(Func<SqliteConnection, Task> action, CancellationToken token)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            await _OperationSemaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using SqliteConnection connection = await OpenConnectionAsync(token).ConfigureAwait(false);
                await ExecuteStatementAsync(connection, "BEGIN IMMEDIATE", token).ConfigureAwait(false);
                try
                {
                    await action(connection).ConfigureAwait(false);
                    await ExecuteStatementAsync(connection, "COMMIT", token).ConfigureAwait(false);
                }
                catch
                {
                    try
                    {
                        await ExecuteStatementAsync(connection, "ROLLBACK", CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    throw;
                }
            }
            finally
            {
                _OperationSemaphore.Release();
            }
        }

        internal static string ToStorageDate(DateTime value)
        {
            return value.ToUniversalTime().ToString("O");
        }

        internal static DateTime FromStorageDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return DateTime.UtcNow;
            return DateTime.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind).ToUniversalTime();
        }

        internal static int ToInt(bool value)
        {
            return value ? 1 : 0;
        }

        internal static bool ToBool(long value)
        {
            return value != 0;
        }

        internal static string NewId(string prefix)
        {
            return prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be disposed.</param>
        protected override void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
                _OperationSemaphore.Dispose();

            _Disposed = true;
            base.Dispose(disposing);
        }

        private static async Task ExecutePragmaAsync(SqliteConnection connection, string statement, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        private static async Task ExecuteStatementAsync(SqliteConnection connection, string statement, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        private static async Task<long> ExecuteScalarLongAsync(SqliteConnection connection, string statement, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = statement;
            object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (result == null || result == DBNull.Value) return 0;
            return Convert.ToInt64(result);
        }

        private async Task ApplyMigrationsAsync(SqliteConnection connection, CancellationToken token)
        {
            List<SchemaMigration> migrations = SqliteSchemaQueries.GetMigrations();
            foreach (SchemaMigration migration in migrations)
            {
                token.ThrowIfCancellationRequested();

                using SqliteCommand check = connection.CreateCommand();
                check.CommandText = "SELECT COUNT(*) FROM schema_migrations WHERE version = $version";
                check.Parameters.AddWithValue("$version", migration.Version);

                bool applied = false;
                try
                {
                    object result = await check.ExecuteScalarAsync(token).ConfigureAwait(false);
                    applied = Convert.ToInt64(result) > 0;
                }
                catch (SqliteException)
                {
                    applied = false;
                }

                if (applied) continue;

                using SqliteTransaction transaction = connection.BeginTransaction();
                foreach (string statement in migration.Statements)
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = statement;
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                using SqliteCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO schema_migrations (version, description, applied_utc, checksum) VALUES ($version, $description, $applied_utc, $checksum)";
                insert.Parameters.AddWithValue("$version", migration.Version);
                insert.Parameters.AddWithValue("$description", migration.Description);
                insert.Parameters.AddWithValue("$applied_utc", ToStorageDate(DateTime.UtcNow));
                insert.Parameters.AddWithValue("$checksum", DBNull.Value);
                await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                transaction.Commit();
            }
        }

        private static async Task SeedSetupStateAsync(SqliteConnection connection, CancellationToken token)
        {
            long count = await ExecuteScalarLongAsync(connection, "SELECT COUNT(*) FROM setup_state WHERE id = 'default'", token).ConfigureAwait(false);
            if (count > 0) return;

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO setup_state (id, status, current_step, selected_provider_id, selected_database_id, completed_utc, dismissed_utc, updated_utc) VALUES ('default', 'NotStarted', NULL, NULL, NULL, NULL, NULL, $updated_utc)";
            command.Parameters.AddWithValue("$updated_utc", ToStorageDate(DateTime.UtcNow));
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        private async Task SeedDefaultsAsync(CancellationToken token)
        {
            long providerCount;
            long databaseCount;

            providerCount = await ExecuteReadAsync(async connection =>
            {
                return await ExecuteScalarLongAsync(connection, "SELECT COUNT(*) FROM model_providers", token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            databaseCount = await ExecuteReadAsync(async connection =>
            {
                return await ExecuteScalarLongAsync(connection, "SELECT COUNT(*) FROM database_connections", token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            if (providerCount == 0)
            {
                await ModelProviders.CreateAsync(DefaultDataFactory.CreateOllamaProvider(), token).ConfigureAwait(false);
                await ModelProviders.CreateAsync(DefaultDataFactory.CreateOpenAiProvider(), token).ConfigureAwait(false);
                await ModelProviders.CreateAsync(DefaultDataFactory.CreateOpenAiCompatibleProvider(), token).ConfigureAwait(false);
                await ModelProviders.CreateAsync(DefaultDataFactory.CreateGeminiProvider(), token).ConfigureAwait(false);
            }

            if (databaseCount == 0)
                await DatabaseConnections.CreateAsync(DefaultDataFactory.CreateSampleDatabase(), token).ConfigureAwait(false);
        }
    }
}
