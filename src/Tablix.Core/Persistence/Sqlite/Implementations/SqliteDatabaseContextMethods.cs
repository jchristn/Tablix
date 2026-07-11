namespace Tablix.Core.Persistence.Sqlite.Implementations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.Persistence.Interfaces;

    /// <summary>
    /// SQLite database-level context persistence methods.
    /// </summary>
    public class SqliteDatabaseContextMethods : IDatabaseContextMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">SQLite persistence driver.</param>
        public SqliteDatabaseContextMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Read database context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Context text.</returns>
        public async Task<string> ReadAsync(string databaseId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(databaseId)) return null;

            using SqliteConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT context FROM context_records WHERE database_id = $database_id AND table_id IS NULL AND scope = 'Database'";
            command.Parameters.AddWithValue("$database_id", databaseId);
            object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (result == null || result == DBNull.Value) return null;
            return Convert.ToString(result);
        }

        /// <summary>
        /// Upsert database context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="context">Context text.</param>
        /// <param name="mode">Update mode.</param>
        /// <param name="source">Context source.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated context.</returns>
        public async Task<string> UpsertAsync(string databaseId, string context, string mode = "replace", string source = "user", CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(databaseId)) throw new ArgumentNullException(nameof(databaseId));

            string existing = await ReadAsync(databaseId, token).ConfigureAwait(false);
            string updated = BuildUpdatedContext(existing, context, mode);

            await _Driver.ExecuteWriteAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO context_records (id, database_id, table_id, scope, context, source, provider_id, prompt, created_utc, updated_utc) VALUES ($id, $database_id, NULL, 'Database', $context, $source, NULL, NULL, $now, $now) ON CONFLICT(database_id, scope) WHERE table_id IS NULL DO UPDATE SET context = excluded.context, source = excluded.source, updated_utc = excluded.updated_utc";
                command.Parameters.AddWithValue("$id", SqliteDatabaseDriver.NewId("ctx"));
                command.Parameters.AddWithValue("$database_id", databaseId);
                command.Parameters.AddWithValue("$context", updated ?? string.Empty);
                command.Parameters.AddWithValue("$source", source ?? "user");
                command.Parameters.AddWithValue("$now", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                using SqliteCommand updateDatabase = connection.CreateCommand();
                updateDatabase.CommandText = "UPDATE database_connections SET updated_utc = $updated_utc WHERE lower(id) = lower($database_id)";
                updateDatabase.Parameters.AddWithValue("$updated_utc", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                updateDatabase.Parameters.AddWithValue("$database_id", databaseId);
                await updateDatabase.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return updated;
        }

        private static string BuildUpdatedContext(string existing, string context, string mode)
        {
            if (string.Equals(mode, "append", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(existing)) return context;
                if (string.IsNullOrWhiteSpace(context)) return existing;
                return existing.TrimEnd() + Environment.NewLine + Environment.NewLine + context;
            }

            if (!string.IsNullOrWhiteSpace(mode) && !string.Equals(mode, "replace", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Unsupported context update mode '" + mode + "'.", nameof(mode));

            return context;
        }
    }
}
