namespace Tablix.Core.Persistence.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence.Sqlite;
    using Tablix.Core.Persistence.Interfaces;

    /// <summary>
    /// SQLite crawled database metadata persistence methods.
    /// </summary>
    public class SqliteDatabaseMetadataMethods : IDatabaseMetadataMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">SQLite persistence driver.</param>
        public SqliteDatabaseMetadataMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Save a crawl result.
        /// </summary>
        /// <param name="detail">Crawl detail.</param>
        /// <param name="token">Cancellation token.</param>
        public async Task SaveCrawlAsync(DatabaseDetail detail, CancellationToken token = default)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));
            if (string.IsNullOrWhiteSpace(detail.DatabaseId)) throw new ArgumentException("DatabaseId is required.", nameof(detail));

            await _Driver.ExecuteWriteAsync(async connection =>
            {
                DateTime now = DateTime.UtcNow;
                int relationshipCount = CountRelationships(detail);

                using SqliteCommand crawl = connection.CreateCommand();
                crawl.CommandText = "INSERT INTO database_crawls (id, database_id, started_utc, completed_utc, success, table_count, relationship_count, error) VALUES ($id, $database_id, $started_utc, $completed_utc, $success, $table_count, $relationship_count, $error)";
                crawl.Parameters.AddWithValue("$id", SqliteDatabaseDriver.NewId("crawl"));
                crawl.Parameters.AddWithValue("$database_id", detail.DatabaseId);
                crawl.Parameters.AddWithValue("$started_utc", SqliteDatabaseDriver.ToStorageDate(detail.CrawledUtc ?? now));
                crawl.Parameters.AddWithValue("$completed_utc", detail.IsCrawled ? (object)SqliteDatabaseDriver.ToStorageDate(detail.CrawledUtc ?? now) : DBNull.Value);
                crawl.Parameters.AddWithValue("$success", SqliteDatabaseDriver.ToInt(detail.IsCrawled));
                crawl.Parameters.AddWithValue("$table_count", detail.Tables.Count);
                crawl.Parameters.AddWithValue("$relationship_count", relationshipCount);
                crawl.Parameters.AddWithValue("$error", (object)detail.CrawlError ?? DBNull.Value);
                await crawl.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                if (!detail.IsCrawled) return;

                foreach (TableDetail table in detail.Tables)
                {
                    string tableId = CreateTableId(detail.DatabaseId, table.SchemaName, table.TableName);
                    table.TableId = tableId;
                    await UpsertTableAsync(connection, detail.DatabaseId, tableId, table, detail.CrawledUtc ?? now, token).ConfigureAwait(false);
                    await ReplaceColumnsAsync(connection, tableId, table, token).ConfigureAwait(false);
                    await ReplaceIndexesAsync(connection, tableId, table, token).ConfigureAwait(false);
                    await ReplaceForeignKeysAsync(connection, tableId, table, token).ConfigureAwait(false);
                }
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read the latest persisted database detail.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database detail or null.</returns>
        public async Task<DatabaseDetail> ReadDetailAsync(string databaseId, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(databaseId)) return null;

            using SqliteConnection connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false);
            DatabaseDetail detail = new DatabaseDetail
            {
                DatabaseId = databaseId,
                IsCrawled = false
            };

            using (SqliteCommand crawl = connection.CreateCommand())
            {
                crawl.CommandText = "SELECT * FROM database_crawls WHERE database_id = $database_id ORDER BY started_utc DESC LIMIT 1";
                crawl.Parameters.AddWithValue("$database_id", databaseId);
                using SqliteDataReader crawlReader = await crawl.ExecuteReaderAsync(token).ConfigureAwait(false);
                if (await crawlReader.ReadAsync(token).ConfigureAwait(false))
                {
                    detail.IsCrawled = SqliteDatabaseDriver.ToBool(Convert.ToInt64(crawlReader["success"]));
                    detail.CrawlError = crawlReader["error"] == DBNull.Value ? null : Convert.ToString(crawlReader["error"]);
                    detail.CrawledUtc = crawlReader["completed_utc"] == DBNull.Value ? (DateTime?)null : SqliteDatabaseDriver.FromStorageDate(Convert.ToString(crawlReader["completed_utc"]));
                }
            }

            detail.Tables = await ReadTablesAsync(connection, databaseId, token).ConfigureAwait(false);
            if (detail.Tables.Count == 0 && !detail.IsCrawled && string.IsNullOrWhiteSpace(detail.CrawlError))
                return null;

            return detail;
        }

        internal static string CreateTableId(string databaseId, string schemaName, string tableName)
        {
            string raw = (databaseId ?? string.Empty) + "_" + (schemaName ?? string.Empty) + "_" + (tableName ?? string.Empty);
            char[] chars = raw.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
            return "tbl_" + new string(chars).Trim('_');
        }

        private static int CountRelationships(DatabaseDetail detail)
        {
            int count = 0;
            foreach (TableDetail table in detail.Tables)
            {
                count += table.ForeignKeys.Count;
            }

            return count;
        }

        private static async Task UpsertTableAsync(SqliteConnection connection, string databaseId, string tableId, TableDetail table, DateTime crawledUtc, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "INSERT INTO database_tables (id, database_id, schema_name, table_name, table_type, row_count, description, last_crawled_utc) VALUES ($id, $database_id, $schema_name, $table_name, NULL, NULL, NULL, $last_crawled_utc) ON CONFLICT(database_id, schema_name, table_name) DO UPDATE SET last_crawled_utc = excluded.last_crawled_utc";
            command.Parameters.AddWithValue("$id", tableId);
            command.Parameters.AddWithValue("$database_id", databaseId);
            command.Parameters.AddWithValue("$schema_name", (object)table.SchemaName ?? DBNull.Value);
            command.Parameters.AddWithValue("$table_name", table.TableName);
            command.Parameters.AddWithValue("$last_crawled_utc", SqliteDatabaseDriver.ToStorageDate(crawledUtc));
            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        private static async Task ReplaceColumnsAsync(SqliteConnection connection, string tableId, TableDetail table, CancellationToken token)
        {
            using SqliteCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM database_columns WHERE table_id = $table_id";
            delete.Parameters.AddWithValue("$table_id", tableId);
            await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            int ordinal = 0;
            foreach (ColumnDetail column in table.Columns)
            {
                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO database_columns (id, table_id, column_name, ordinal, data_type, is_nullable, is_primary_key, default_value, max_length, precision_value, scale_value) VALUES ($id, $table_id, $column_name, $ordinal, $data_type, $is_nullable, $is_primary_key, $default_value, $max_length, NULL, NULL)";
                insert.Parameters.AddWithValue("$id", SqliteDatabaseDriver.NewId("col"));
                insert.Parameters.AddWithValue("$table_id", tableId);
                insert.Parameters.AddWithValue("$column_name", column.ColumnName);
                insert.Parameters.AddWithValue("$ordinal", ordinal);
                insert.Parameters.AddWithValue("$data_type", (object)column.DataType ?? DBNull.Value);
                insert.Parameters.AddWithValue("$is_nullable", SqliteDatabaseDriver.ToInt(column.IsNullable));
                insert.Parameters.AddWithValue("$is_primary_key", SqliteDatabaseDriver.ToInt(column.IsPrimaryKey));
                insert.Parameters.AddWithValue("$default_value", (object)column.DefaultValue ?? DBNull.Value);
                insert.Parameters.AddWithValue("$max_length", column.MaxLength.HasValue ? (object)column.MaxLength.Value : DBNull.Value);
                await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                ordinal++;
            }
        }

        private static async Task ReplaceIndexesAsync(SqliteConnection connection, string tableId, TableDetail table, CancellationToken token)
        {
            using SqliteCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM database_indexes WHERE table_id = $table_id";
            delete.Parameters.AddWithValue("$table_id", tableId);
            await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            foreach (IndexDetail index in table.Indexes)
            {
                string indexId = SqliteDatabaseDriver.NewId("idx");
                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO database_indexes (id, table_id, index_name, is_unique) VALUES ($id, $table_id, $index_name, $is_unique)";
                insert.Parameters.AddWithValue("$id", indexId);
                insert.Parameters.AddWithValue("$table_id", tableId);
                insert.Parameters.AddWithValue("$index_name", index.IndexName);
                insert.Parameters.AddWithValue("$is_unique", SqliteDatabaseDriver.ToInt(index.IsUnique));
                await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);

                int ordinal = 0;
                foreach (string column in index.Columns)
                {
                    using SqliteCommand columnInsert = connection.CreateCommand();
                    columnInsert.CommandText = "INSERT INTO database_index_columns (index_id, column_name, ordinal) VALUES ($index_id, $column_name, $ordinal)";
                    columnInsert.Parameters.AddWithValue("$index_id", indexId);
                    columnInsert.Parameters.AddWithValue("$column_name", column);
                    columnInsert.Parameters.AddWithValue("$ordinal", ordinal);
                    await columnInsert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    ordinal++;
                }
            }
        }

        private static async Task ReplaceForeignKeysAsync(SqliteConnection connection, string tableId, TableDetail table, CancellationToken token)
        {
            using SqliteCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM database_foreign_keys WHERE table_id = $table_id";
            delete.Parameters.AddWithValue("$table_id", tableId);
            await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);

            foreach (ForeignKeyDetail foreignKey in table.ForeignKeys)
            {
                using SqliteCommand insert = connection.CreateCommand();
                insert.CommandText = "INSERT INTO database_foreign_keys (id, table_id, constraint_name, column_name, referenced_table, referenced_column) VALUES ($id, $table_id, $constraint_name, $column_name, $referenced_table, $referenced_column)";
                insert.Parameters.AddWithValue("$id", SqliteDatabaseDriver.NewId("fk"));
                insert.Parameters.AddWithValue("$table_id", tableId);
                insert.Parameters.AddWithValue("$constraint_name", (object)foreignKey.ConstraintName ?? DBNull.Value);
                insert.Parameters.AddWithValue("$column_name", foreignKey.ColumnName);
                insert.Parameters.AddWithValue("$referenced_table", foreignKey.ReferencedTable);
                insert.Parameters.AddWithValue("$referenced_column", foreignKey.ReferencedColumn);
                await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task<List<TableDetail>> ReadTablesAsync(SqliteConnection connection, string databaseId, CancellationToken token)
        {
            List<SqliteTableRow> rows = new List<SqliteTableRow>();
            List<TableDetail> tables = new List<TableDetail>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT t.*, c.context AS table_context FROM database_tables t LEFT JOIN context_records c ON c.database_id = t.database_id AND c.table_id = t.id AND c.scope = 'Table' WHERE t.database_id = $database_id ORDER BY t.schema_name, t.table_name";
            command.Parameters.AddWithValue("$database_id", databaseId);
            using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    rows.Add(new SqliteTableRow
                    {
                        TableId = Convert.ToString(reader["id"]),
                        SchemaName = reader["schema_name"] == DBNull.Value ? null : Convert.ToString(reader["schema_name"]),
                        TableName = Convert.ToString(reader["table_name"]),
                        Context = reader["table_context"] == DBNull.Value ? null : Convert.ToString(reader["table_context"])
                    });
                }
            }

            foreach (SqliteTableRow row in rows)
            {
                TableDetail table = new TableDetail
                {
                    TableId = row.TableId,
                    SchemaName = row.SchemaName,
                    TableName = row.TableName,
                    Context = row.Context
                };
                table.Columns = await ReadColumnsAsync(connection, row.TableId, token).ConfigureAwait(false);
                table.Indexes = await ReadIndexesAsync(connection, row.TableId, token).ConfigureAwait(false);
                table.ForeignKeys = await ReadForeignKeysAsync(connection, row.TableId, token).ConfigureAwait(false);
                tables.Add(table);
            }

            return tables;
        }

        private static async Task<List<ColumnDetail>> ReadColumnsAsync(SqliteConnection connection, string tableId, CancellationToken token)
        {
            List<ColumnDetail> columns = new List<ColumnDetail>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM database_columns WHERE table_id = $table_id ORDER BY ordinal";
            command.Parameters.AddWithValue("$table_id", tableId);
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                columns.Add(new ColumnDetail
                {
                    ColumnName = Convert.ToString(reader["column_name"]),
                    DataType = reader["data_type"] == DBNull.Value ? null : Convert.ToString(reader["data_type"]),
                    IsNullable = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["is_nullable"])),
                    IsPrimaryKey = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["is_primary_key"])),
                    DefaultValue = reader["default_value"] == DBNull.Value ? null : Convert.ToString(reader["default_value"]),
                    MaxLength = reader["max_length"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["max_length"])
                });
            }

            return columns;
        }

        private static async Task<List<IndexDetail>> ReadIndexesAsync(SqliteConnection connection, string tableId, CancellationToken token)
        {
            List<IndexDetail> indexes = new List<IndexDetail>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM database_indexes WHERE table_id = $table_id ORDER BY index_name";
            command.Parameters.AddWithValue("$table_id", tableId);
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                string indexId = Convert.ToString(reader["id"]);
                IndexDetail index = new IndexDetail
                {
                    IndexName = Convert.ToString(reader["index_name"]),
                    IsUnique = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["is_unique"])),
                    Columns = await ReadIndexColumnsAsync(connection, indexId, token).ConfigureAwait(false)
                };
                indexes.Add(index);
            }

            return indexes;
        }

        private static async Task<List<string>> ReadIndexColumnsAsync(SqliteConnection connection, string indexId, CancellationToken token)
        {
            List<string> columns = new List<string>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT column_name FROM database_index_columns WHERE index_id = $index_id ORDER BY ordinal";
            command.Parameters.AddWithValue("$index_id", indexId);
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                columns.Add(Convert.ToString(reader["column_name"]));
            }

            return columns;
        }

        private static async Task<List<ForeignKeyDetail>> ReadForeignKeysAsync(SqliteConnection connection, string tableId, CancellationToken token)
        {
            List<ForeignKeyDetail> foreignKeys = new List<ForeignKeyDetail>();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM database_foreign_keys WHERE table_id = $table_id ORDER BY constraint_name, column_name";
            command.Parameters.AddWithValue("$table_id", tableId);
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                foreignKeys.Add(new ForeignKeyDetail
                {
                    ConstraintName = reader["constraint_name"] == DBNull.Value ? null : Convert.ToString(reader["constraint_name"]),
                    ColumnName = Convert.ToString(reader["column_name"]),
                    ReferencedTable = Convert.ToString(reader["referenced_table"]),
                    ReferencedColumn = Convert.ToString(reader["referenced_column"])
                });
            }

            return foreignKeys;
        }
    }
}
