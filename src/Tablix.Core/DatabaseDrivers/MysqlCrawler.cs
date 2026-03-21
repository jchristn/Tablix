namespace Tablix.Core.DatabaseDrivers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using MySqlConnector;
    using SerializableDataTables;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// MySQL database crawler and query executor.
    /// </summary>
    public class MysqlCrawler : IDatabaseCrawler
    {
        #region Public-Methods

        /// <inheritdoc />
        public async Task<DatabaseDetail> CrawlAsync(DatabaseEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string schema = entry.DatabaseName;
            if (!String.IsNullOrEmpty(entry.Schema)
                && !String.Equals(entry.Schema, "public", StringComparison.OrdinalIgnoreCase))
            {
                schema = entry.Schema;
            }

            DatabaseDetail detail = new DatabaseDetail
            {
                DatabaseId = entry.Id,
                Type = DatabaseTypeEnum.Mysql,
                DatabaseName = entry.DatabaseName,
                Schema = schema,
                Context = entry.Context
            };

            string connectionString = BuildConnectionString(entry);

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                List<string> tableNames = await GetTableNamesAsync(connection, schema, token).ConfigureAwait(false);

                foreach (string tableName in tableNames)
                {
                    TableDetail table = new TableDetail
                    {
                        TableName = tableName,
                        SchemaName = schema
                    };

                    table.Columns = await GetColumnsAsync(connection, schema, tableName, token).ConfigureAwait(false);

                    List<string> primaryKeys = await GetPrimaryKeysAsync(connection, schema, tableName, token).ConfigureAwait(false);
                    foreach (ColumnDetail column in table.Columns)
                    {
                        if (primaryKeys.Contains(column.ColumnName))
                            column.IsPrimaryKey = true;
                    }

                    table.ForeignKeys = await GetForeignKeysAsync(connection, schema, tableName, token).ConfigureAwait(false);
                    table.Indexes = await GetIndexesAsync(connection, schema, tableName, token).ConfigureAwait(false);

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

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (MySqlCommand command = new MySqlCommand(query, connection))
                {
                    using (MySqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private string BuildConnectionString(DatabaseEntry entry)
        {
            return "Server=" + entry.Hostname
                + ";Port=" + entry.Port
                + ";Database=" + entry.DatabaseName
                + ";User=" + entry.User
                + ";Password=" + entry.Password;
        }

        private async Task<List<string>> GetTableNamesAsync(MySqlConnection connection, string schema, CancellationToken token)
        {
            List<string> tables = new List<string>();

            using (MySqlCommand command = new MySqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema AND table_type = 'BASE TABLE'",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);

                using (MySqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            return tables;
        }

        private async Task<List<ColumnDetail>> GetColumnsAsync(MySqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<ColumnDetail> columns = new List<ColumnDetail>();

            using (MySqlCommand command = new MySqlCommand(
                "SELECT column_name, data_type, is_nullable, column_default, character_maximum_length "
                + "FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (MySqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        ColumnDetail column = new ColumnDetail
                        {
                            ColumnName = reader.GetString(0),
                            DataType = reader.GetString(1),
                            IsNullable = reader.GetString(2) == "YES",
                            DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                            MaxLength = reader.IsDBNull(4) ? null : (int?)reader.GetInt64(4)
                        };

                        columns.Add(column);
                    }
                }
            }

            return columns;
        }

        private async Task<List<string>> GetPrimaryKeysAsync(MySqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<string> primaryKeys = new List<string>();

            using (MySqlCommand command = new MySqlCommand(
                "SELECT kcu.column_name "
                + "FROM information_schema.table_constraints tc "
                + "JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema "
                + "WHERE tc.table_schema = @schema AND tc.table_name = @table AND tc.constraint_type = 'PRIMARY KEY'",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (MySqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        primaryKeys.Add(reader.GetString(0));
                    }
                }
            }

            return primaryKeys;
        }

        private async Task<List<ForeignKeyDetail>> GetForeignKeysAsync(MySqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<ForeignKeyDetail> foreignKeys = new List<ForeignKeyDetail>();

            using (MySqlCommand command = new MySqlCommand(
                "SELECT rc.constraint_name, kcu.column_name, kcu.referenced_table_name, kcu.referenced_column_name "
                + "FROM information_schema.referential_constraints rc "
                + "JOIN information_schema.key_column_usage kcu ON rc.constraint_name = kcu.constraint_name AND rc.constraint_schema = kcu.constraint_schema "
                + "WHERE kcu.table_schema = @schema AND kcu.table_name = @table AND kcu.referenced_table_name IS NOT NULL",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (MySqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        ForeignKeyDetail fk = new ForeignKeyDetail
                        {
                            ConstraintName = reader.GetString(0),
                            ColumnName = reader.GetString(1),
                            ReferencedTable = reader.GetString(2),
                            ReferencedColumn = reader.GetString(3)
                        };

                        foreignKeys.Add(fk);
                    }
                }
            }

            return foreignKeys;
        }

        private async Task<List<IndexDetail>> GetIndexesAsync(MySqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<IndexDetail> indexes = new List<IndexDetail>();
            Dictionary<string, IndexDetail> indexMap = new Dictionary<string, IndexDetail>();

            using (MySqlCommand command = new MySqlCommand(
                "SELECT index_name, column_name, non_unique "
                + "FROM information_schema.statistics "
                + "WHERE table_schema = @schema AND table_name = @table ORDER BY index_name, seq_in_index",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (MySqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string indexName = reader.GetString(0);
                        string columnName = reader.GetString(1);
                        bool isUnique = reader.GetInt32(2) == 0;

                        if (!indexMap.ContainsKey(indexName))
                        {
                            IndexDetail index = new IndexDetail
                            {
                                IndexName = indexName,
                                IsUnique = isUnique
                            };

                            indexMap[indexName] = index;
                            indexes.Add(index);
                        }

                        indexMap[indexName].Columns.Add(columnName);
                    }
                }
            }

            return indexes;
        }

        #endregion
    }
}
