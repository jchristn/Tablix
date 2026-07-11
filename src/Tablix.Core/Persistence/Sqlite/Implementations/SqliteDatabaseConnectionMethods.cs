namespace Tablix.Core.Persistence.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.Enums;
    using Tablix.Core.Persistence.Interfaces;
    using Tablix.Core.Settings;

    /// <summary>
    /// SQLite configured database connection persistence methods.
    /// </summary>
    public class SqliteDatabaseConnectionMethods : IDatabaseConnectionMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">SQLite persistence driver.</param>
        public SqliteDatabaseConnectionMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a database connection.
        /// </summary>
        /// <param name="database">Database connection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created database connection.</returns>
        public async Task<DatabaseEntry> CreateAsync(DatabaseEntry database, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            DateTime now = DateTime.UtcNow;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO database_connections (id, name, type, hostname, port, username, password, database_name, schema_name, filename, context, created_utc, updated_utc, last_connection_test_utc, last_connection_test_success, last_connection_test_message) VALUES ($id, $name, $type, $hostname, $port, $username, $password, $database_name, $schema_name, $filename, $context, $created_utc, $updated_utc, NULL, NULL, NULL)";
                AddDatabaseParameters(command, database, now, now);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                await SaveAllowedQueriesAsync(connection, database, token).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(database.Context))
                    await UpsertContextAsync(connection, database.Id, database.Context, "seed", token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return database;
        }

        /// <summary>
        /// Read a database connection by identifier.
        /// </summary>
        /// <param name="id">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database connection or null.</returns>
        public async Task<DatabaseEntry> ReadAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            using SqliteConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false);
            DatabaseEntry database = await ReadDatabaseAsync(connection, id, token).ConfigureAwait(false);
            return database;
        }

        /// <summary>
        /// Enumerate database connections.
        /// </summary>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="skip">Records to skip.</param>
        /// <param name="filter">Optional filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database connections.</returns>
        public async Task<List<DatabaseEntry>> EnumerateAsync(int maxResults, int skip, string filter = null, CancellationToken token = default)
        {
            int safeMax = Math.Clamp(maxResults, 1, 1000);
            int safeSkip = Math.Max(skip, 0);

            using SqliteConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = BuildDatabaseSelectSql(false, filter) + " ORDER BY dc.name, dc.id LIMIT $limit OFFSET $offset";
            AddFilterParameters(command, filter);
            command.Parameters.AddWithValue("$limit", safeMax);
            command.Parameters.AddWithValue("$offset", safeSkip);

            List<DatabaseEntry> databases = new List<DatabaseEntry>();
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                DatabaseEntry database = ReadDatabase(reader);
                database.AllowedQueries = await ReadAllowedQueriesAsync(connection, database.Id, token).ConfigureAwait(false);
                databases.Add(database);
            }

            return databases;
        }

        /// <summary>
        /// Count database connections.
        /// </summary>
        /// <param name="filter">Optional filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database count.</returns>
        public async Task<long> CountAsync(string filter = null, CancellationToken token = default)
        {
            using SqliteConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = BuildDatabaseSelectSql(true, filter);
            AddFilterParameters(command, filter);
            object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return Convert.ToInt64(result);
        }

        /// <summary>
        /// Update a database connection.
        /// </summary>
        /// <param name="database">Database connection.</param>
        /// <param name="preserveCredentialsWhenNull">Whether to preserve existing credentials when null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated database connection.</returns>
        public async Task<DatabaseEntry> UpdateAsync(DatabaseEntry database, bool preserveCredentialsWhenNull = true, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            DatabaseEntry existing = await ReadAsync(database.Id, token).ConfigureAwait(false);
            if (existing == null) throw new KeyNotFoundException("Database with ID '" + database.Id + "' not found.");
            if (preserveCredentialsWhenNull)
            {
                if (string.IsNullOrEmpty(database.User)) database.User = existing.User;
                if (string.IsNullOrEmpty(database.Password)) database.Password = existing.Password;
            }

            DateTime now = DateTime.UtcNow;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "UPDATE database_connections SET name = $name, type = $type, hostname = $hostname, port = $port, username = $username, password = $password, database_name = $database_name, schema_name = $schema_name, filename = $filename, context = $context, updated_utc = $updated_utc WHERE lower(id) = lower($id)";
                AddDatabaseParameters(command, database, now, now);
                int count = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                if (count == 0) throw new KeyNotFoundException("Database with ID '" + database.Id + "' not found.");
                await SaveAllowedQueriesAsync(connection, database, token).ConfigureAwait(false);
                if (database.Context != null)
                    await UpsertContextAsync(connection, database.Id, database.Context, "user", token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return database;
        }

        /// <summary>
        /// Delete a database connection.
        /// </summary>
        /// <param name="id">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            int count = 0;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM database_connections WHERE lower(id) = lower($id)";
                command.Parameters.AddWithValue("$id", id);
                count = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return count > 0;
        }

        internal static async Task<DatabaseEntry> ReadDatabaseAsync(SqliteConnection connection, string id, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = BuildDatabaseSelectSql(false, null) + " WHERE lower(dc.id) = lower($id)";
            command.Parameters.AddWithValue("$id", id);
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;

            DatabaseEntry database = ReadDatabase(reader);
            database.AllowedQueries = await ReadAllowedQueriesAsync(connection, database.Id, token).ConfigureAwait(false);
            return database;
        }

        private static string BuildDatabaseSelectSql(bool count, string filter)
        {
            string sql = count
                ? "SELECT COUNT(*) FROM database_connections dc"
                : "SELECT dc.*, cr.context AS persisted_context FROM database_connections dc LEFT JOIN context_records cr ON cr.database_id = dc.id AND cr.table_id IS NULL AND cr.scope = 'Database'";
            if (!string.IsNullOrWhiteSpace(filter))
                sql += " WHERE lower(dc.id) LIKE lower($filter) OR lower(dc.name) LIKE lower($filter) OR lower(dc.database_name) LIKE lower($filter)";
            return sql;
        }

        private static void AddFilterParameters(SqliteCommand command, string filter)
        {
            if (!string.IsNullOrWhiteSpace(filter))
                command.Parameters.AddWithValue("$filter", "%" + filter.Trim() + "%");
        }

        private static void AddDatabaseParameters(SqliteCommand command, DatabaseEntry database, DateTime createdUtc, DateTime updatedUtc)
        {
            command.Parameters.AddWithValue("$id", database.Id);
            command.Parameters.AddWithValue("$name", (object)database.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("$type", database.Type.ToString());
            command.Parameters.AddWithValue("$hostname", (object)database.Hostname ?? DBNull.Value);
            command.Parameters.AddWithValue("$port", database.Port.HasValue ? (object)database.Port.Value : DBNull.Value);
            command.Parameters.AddWithValue("$username", (object)database.User ?? DBNull.Value);
            command.Parameters.AddWithValue("$password", (object)database.Password ?? DBNull.Value);
            command.Parameters.AddWithValue("$database_name", (object)database.DatabaseName ?? DBNull.Value);
            command.Parameters.AddWithValue("$schema_name", (object)database.Schema ?? DBNull.Value);
            command.Parameters.AddWithValue("$filename", (object)database.Filename ?? DBNull.Value);
            command.Parameters.AddWithValue("$context", DBNull.Value);
            command.Parameters.AddWithValue("$created_utc", SqliteDatabaseDriver.ToStorageDate(createdUtc));
            command.Parameters.AddWithValue("$updated_utc", SqliteDatabaseDriver.ToStorageDate(updatedUtc));
        }

        private static DatabaseEntry ReadDatabase(SqliteDataReader reader)
        {
            return new DatabaseEntry
            {
                Id = Convert.ToString(reader["id"]),
                Name = reader["name"] == DBNull.Value ? null : Convert.ToString(reader["name"]),
                Type = Enum.Parse<DatabaseTypeEnum>(Convert.ToString(reader["type"])),
                Hostname = reader["hostname"] == DBNull.Value ? null : Convert.ToString(reader["hostname"]),
                Port = reader["port"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["port"]),
                User = reader["username"] == DBNull.Value ? null : Convert.ToString(reader["username"]),
                Password = reader["password"] == DBNull.Value ? null : Convert.ToString(reader["password"]),
                DatabaseName = reader["database_name"] == DBNull.Value ? null : Convert.ToString(reader["database_name"]),
                Schema = reader["schema_name"] == DBNull.Value ? null : Convert.ToString(reader["schema_name"]),
                Filename = reader["filename"] == DBNull.Value ? null : Convert.ToString(reader["filename"]),
                Context = ReadPersistedContext(reader)
            };
        }

        private static string ReadPersistedContext(SqliteDataReader reader)
        {
            int persistedIndex = reader.GetOrdinal("persisted_context");
            if (reader[persistedIndex] != DBNull.Value) return Convert.ToString(reader[persistedIndex]);
            return null;
        }

        private static async Task<List<string>> ReadAllowedQueriesAsync(SqliteConnection connection, string databaseId, CancellationToken token)
        {
            List<string> queries = new List<string>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT query_operation FROM database_allowed_queries WHERE database_id = $database_id ORDER BY query_operation";
            command.Parameters.AddWithValue("$database_id", databaseId);
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                queries.Add(Convert.ToString(reader["query_operation"]));
            }

            return queries;
        }

        private static async Task SaveAllowedQueriesAsync(SqliteConnection connection, DatabaseEntry database, CancellationToken token)
        {
            using SqliteCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM database_allowed_queries WHERE database_id = $database_id";
            delete.Parameters.AddWithValue("$database_id", database.Id);
            await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            foreach (string query in database.AllowedQueries)
            {
                if (string.IsNullOrWhiteSpace(query)) continue;
                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT OR IGNORE INTO database_allowed_queries (database_id, query_operation) VALUES ($database_id, $query_operation)";
                insert.Parameters.AddWithValue("$database_id", database.Id);
                insert.Parameters.AddWithValue("$query_operation", query.Trim().ToUpperInvariant());
                await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task UpsertContextAsync(SqliteConnection connection, string databaseId, string context, string source, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO context_records (id, database_id, table_id, scope, context, source, provider_id, prompt, created_utc, updated_utc) VALUES ($id, $database_id, NULL, 'Database', $context, $source, NULL, NULL, $now, $now) ON CONFLICT(database_id, scope) WHERE table_id IS NULL DO UPDATE SET context = excluded.context, source = excluded.source, updated_utc = excluded.updated_utc";
            command.Parameters.AddWithValue("$id", SqliteDatabaseDriver.NewId("ctx"));
            command.Parameters.AddWithValue("$database_id", databaseId);
            command.Parameters.AddWithValue("$context", (object)context ?? string.Empty);
            command.Parameters.AddWithValue("$source", source ?? "user");
            command.Parameters.AddWithValue("$now", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
    }
}
