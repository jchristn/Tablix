namespace Tablix.Core.Persistence.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence.Interfaces;

    /// <summary>
    /// SQLite table-level context persistence methods.
    /// </summary>
    public class SqliteTableContextMethods : ITableContextMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">SQLite persistence driver.</param>
        public SqliteTableContextMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Read table context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="tableId">Table identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Table context or null.</returns>
        public async Task<TableContextRead> ReadAsync(string databaseId, string tableId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(databaseId) || string.IsNullOrWhiteSpace(tableId)) return null;

            return await _Driver.ExecuteReadAsync(async connection =>
            {
                return await ReadTableContextAsync(connection, databaseId, tableId, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate table contexts for a database.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Table contexts.</returns>
        public async Task<List<TableContextRead>> EnumerateAsync(string databaseId, CancellationToken token = default)
        {
            List<TableContextRead> contexts = new List<TableContextRead>();
            if (string.IsNullOrWhiteSpace(databaseId)) return contexts;

            return await _Driver.ExecuteReadAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "SELECT c.*, t.schema_name, t.table_name FROM context_records c INNER JOIN database_tables t ON t.id = c.table_id WHERE c.database_id = $database_id AND c.scope = 'Table' ORDER BY t.schema_name, t.table_name";
                command.Parameters.AddWithValue("$database_id", databaseId);
                using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    contexts.Add(ReadTableContext(reader));
                }

                return contexts;
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Upsert table context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="tableId">Table identifier.</param>
        /// <param name="context">Context text.</param>
        /// <param name="mode">Update mode.</param>
        /// <param name="source">Context source.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated table context.</returns>
        public async Task<TableContextRead> UpsertAsync(string databaseId, string tableId, string context, string mode = "replace", string source = "user", CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(databaseId)) throw new ArgumentNullException(nameof(databaseId));
            if (string.IsNullOrWhiteSpace(tableId)) throw new ArgumentNullException(nameof(tableId));

            TableContextRead updatedRead = null;

            await _Driver.ExecuteWriteAsync(async connection =>
            {
                if (!await TableExistsAsync(connection, databaseId, tableId, token).ConfigureAwait(false))
                    throw new KeyNotFoundException("Table metadata '" + tableId + "' was not found for database '" + databaseId + "'. Crawl the database again before writing table context.");

                TableContextRead existing = await ReadTableContextAsync(connection, databaseId, tableId, token).ConfigureAwait(false);
                string updated = BuildUpdatedContext(existing == null ? null : existing.Context, context, mode);

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO context_records (id, database_id, table_id, scope, context, source, provider_id, prompt, created_utc, updated_utc) VALUES ($id, $database_id, $table_id, 'Table', $context, $source, NULL, NULL, $now, $now) ON CONFLICT(database_id, table_id, scope) WHERE table_id IS NOT NULL DO UPDATE SET context = excluded.context, source = excluded.source, updated_utc = excluded.updated_utc";
                command.Parameters.AddWithValue("$id", SqliteDatabaseDriver.NewId("ctx"));
                command.Parameters.AddWithValue("$database_id", databaseId);
                command.Parameters.AddWithValue("$table_id", tableId);
                command.Parameters.AddWithValue("$context", updated ?? string.Empty);
                command.Parameters.AddWithValue("$source", source ?? "user");
                command.Parameters.AddWithValue("$now", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                updatedRead = await ReadTableContextAsync(connection, databaseId, tableId, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return updatedRead;
        }

        private static async Task<bool> TableExistsAsync(SqliteConnection connection, string databaseId, string tableId, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM database_tables WHERE database_id = $database_id AND id = $table_id";
            command.Parameters.AddWithValue("$database_id", databaseId);
            command.Parameters.AddWithValue("$table_id", tableId);
            object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
            return Convert.ToInt64(result) > 0;
        }

        private static async Task<TableContextRead> ReadTableContextAsync(SqliteConnection connection, string databaseId, string tableId, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT c.*, t.schema_name, t.table_name FROM context_records c INNER JOIN database_tables t ON t.id = c.table_id WHERE c.database_id = $database_id AND c.table_id = $table_id AND c.scope = 'Table'";
            command.Parameters.AddWithValue("$database_id", databaseId);
            command.Parameters.AddWithValue("$table_id", tableId);
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
                return ReadTableContext(reader);

            return null;
        }

        private static TableContextRead ReadTableContext(SqliteDataReader reader)
        {
            return new TableContextRead
            {
                Id = Convert.ToString(reader["id"]),
                DatabaseId = Convert.ToString(reader["database_id"]),
                TableId = Convert.ToString(reader["table_id"]),
                SchemaName = reader["schema_name"] == DBNull.Value ? null : Convert.ToString(reader["schema_name"]),
                TableName = Convert.ToString(reader["table_name"]),
                Context = Convert.ToString(reader["context"]),
                Source = Convert.ToString(reader["source"]),
                UpdatedUtc = SqliteDatabaseDriver.FromStorageDate(Convert.ToString(reader["updated_utc"]))
            };
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
