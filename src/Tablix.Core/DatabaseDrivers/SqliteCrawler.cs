namespace Tablix.Core.DatabaseDrivers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using SerializableDataTables;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// SQLite database crawler and query executor.
    /// </summary>
    public class SqliteCrawler : IDatabaseCrawler
    {
        #region Public-Methods

        /// <inheritdoc />
        public async Task<DatabaseDetail> CrawlAsync(DatabaseEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            DatabaseDetail detail = new DatabaseDetail
            {
                DatabaseId = entry.Id,
                Type = DatabaseTypeEnum.Sqlite,
                DatabaseName = entry.DatabaseName ?? entry.Filename,
                Schema = entry.Schema,
                Context = entry.Context
            };

            string connectionString = BuildConnectionString(entry);

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                List<string> tableNames = await GetTableNamesAsync(connection, token).ConfigureAwait(false);

                foreach (string tableName in tableNames)
                {
                    TableDetail table = new TableDetail
                    {
                        TableName = tableName,
                        SchemaName = "main"
                    };

                    table.Columns = await GetColumnsAsync(connection, tableName, token).ConfigureAwait(false);
                    table.ForeignKeys = await GetForeignKeysAsync(connection, tableName, token).ConfigureAwait(false);
                    table.Indexes = await GetIndexesAsync(connection, tableName, token).ConfigureAwait(false);

                    detail.Tables.Add(table);
                }

                detail.IsCrawled = true;
                detail.CrawledUtc = DateTime.UtcNow;
            }

            return detail;
        }

        /// <inheritdoc />
        public async Task<QueryResult> ExecuteQueryAsync(DatabaseEntry entry, string query, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (String.IsNullOrWhiteSpace(query)) throw new ArgumentNullException(nameof(query));

            Stopwatch stopwatch = Stopwatch.StartNew();
            string connectionString = BuildConnectionString(entry);

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqliteCommand command = new SqliteCommand(query, connection))
                {
                    using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        DataTable dataTable = new DataTable("Results");
                        dataTable.Load(reader);

                        stopwatch.Stop();

                        SerializableDataTable serializableTable = SerializableDataTable.FromDataTable(dataTable);

                        return new QueryResult
                        {
                            Success = true,
                            DatabaseId = entry.Id,
                            RowsReturned = dataTable.Rows.Count,
                            TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                            Data = serializableTable
                        };
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task TestConnectionAsync(DatabaseEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string connectionString = BuildConnectionString(entry);

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private string BuildConnectionString(DatabaseEntry entry)
        {
            if (String.IsNullOrEmpty(entry.Filename))
                throw new ArgumentException("Filename is required for SQLite databases.");

            return "Data Source=" + entry.Filename;
        }

        private async Task<List<string>> GetTableNamesAsync(SqliteConnection connection, CancellationToken token)
        {
            List<string> tables = new List<string>();

            using (SqliteCommand command = new SqliteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
                connection))
            {
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            return tables;
        }

        private async Task<List<ColumnDetail>> GetColumnsAsync(SqliteConnection connection, string tableName, CancellationToken token)
        {
            List<ColumnDetail> columns = new List<ColumnDetail>();

            using (SqliteCommand command = new SqliteCommand("PRAGMA table_info('" + tableName + "')", connection))
            {
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        ColumnDetail column = new ColumnDetail
                        {
                            ColumnName = reader.GetString(1),
                            DataType = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2),
                            IsNullable = reader.GetInt32(3) == 0,
                            IsPrimaryKey = reader.GetInt32(5) > 0,
                            DefaultValue = reader.IsDBNull(4) ? null : reader.GetString(4)
                        };

                        columns.Add(column);
                    }
                }
            }

            return columns;
        }

        private async Task<List<ForeignKeyDetail>> GetForeignKeysAsync(SqliteConnection connection, string tableName, CancellationToken token)
        {
            List<ForeignKeyDetail> foreignKeys = new List<ForeignKeyDetail>();

            using (SqliteCommand command = new SqliteCommand("PRAGMA foreign_key_list('" + tableName + "')", connection))
            {
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        ForeignKeyDetail fk = new ForeignKeyDetail
                        {
                            ConstraintName = "fk_" + tableName + "_" + reader.GetString(3),
                            ColumnName = reader.GetString(3),
                            ReferencedTable = reader.GetString(2),
                            ReferencedColumn = reader.GetString(4)
                        };

                        foreignKeys.Add(fk);
                    }
                }
            }

            return foreignKeys;
        }

        private async Task<List<IndexDetail>> GetIndexesAsync(SqliteConnection connection, string tableName, CancellationToken token)
        {
            List<IndexDetail> indexes = new List<IndexDetail>();

            using (SqliteCommand command = new SqliteCommand("PRAGMA index_list('" + tableName + "')", connection))
            {
                using (SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string indexName = reader.GetString(1);
                        bool isUnique = reader.GetInt32(2) == 1;

                        IndexDetail index = new IndexDetail
                        {
                            IndexName = indexName,
                            IsUnique = isUnique
                        };

                        // Get columns for this index
                        using (SqliteCommand colCommand = new SqliteCommand("PRAGMA index_info('" + indexName + "')", connection))
                        {
                            using (SqliteDataReader colReader = await colCommand.ExecuteReaderAsync(token).ConfigureAwait(false))
                            {
                                while (await colReader.ReadAsync(token).ConfigureAwait(false))
                                {
                                    index.Columns.Add(colReader.GetString(2));
                                }
                            }
                        }

                        indexes.Add(index);
                    }
                }
            }

            return indexes;
        }

        #endregion
    }
}
