namespace Tablix.Core.DatabaseDrivers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using SerializableDataTables;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// PostgreSQL database crawler and query executor.
    /// </summary>
    public class PostgresCrawler : IDatabaseCrawler
    {
        #region Public-Methods

        /// <inheritdoc />
        public async Task<DatabaseDetail> CrawlAsync(DatabaseEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string schema = entry.Schema ?? "public";

            DatabaseDetail detail = new DatabaseDetail
            {
                DatabaseId = entry.Id,
                Type = DatabaseTypeEnum.Postgresql,
                DatabaseName = entry.DatabaseName,
                Schema = schema,
                Context = entry.Context
            };

            string connectionString = BuildConnectionString(entry);

            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
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

            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (NpgsqlCommand command = new NpgsqlCommand(query, connection))
                {
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (NpgsqlConnection connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private string BuildConnectionString(DatabaseEntry entry)
        {
            return "Host=" + entry.Hostname
                + ";Port=" + entry.Port
                + ";Database=" + entry.DatabaseName
                + ";Username=" + entry.User
                + ";Password=" + entry.Password;
        }

        private async Task<List<string>> GetTableNamesAsync(NpgsqlConnection connection, string schema, CancellationToken token)
        {
            List<string> tables = new List<string>();

            using (NpgsqlCommand command = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.tables WHERE table_schema = @schema AND table_type = 'BASE TABLE'",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);

                using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            return tables;
        }

        private async Task<List<ColumnDetail>> GetColumnsAsync(NpgsqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<ColumnDetail> columns = new List<ColumnDetail>();

            using (NpgsqlCommand command = new NpgsqlCommand(
                "SELECT column_name, data_type, is_nullable, column_default, character_maximum_length "
                + "FROM information_schema.columns WHERE table_schema = @schema AND table_name = @table",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        ColumnDetail column = new ColumnDetail
                        {
                            ColumnName = reader.GetString(0),
                            DataType = reader.GetString(1),
                            IsNullable = reader.GetString(2) == "YES",
                            DefaultValue = reader.IsDBNull(3) ? null : reader.GetString(3),
                            MaxLength = reader.IsDBNull(4) ? null : reader.GetInt32(4)
                        };

                        columns.Add(column);
                    }
                }
            }

            return columns;
        }

        private async Task<List<string>> GetPrimaryKeysAsync(NpgsqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<string> primaryKeys = new List<string>();

            using (NpgsqlCommand command = new NpgsqlCommand(
                "SELECT kcu.column_name "
                + "FROM information_schema.table_constraints tc "
                + "JOIN information_schema.key_column_usage kcu ON tc.constraint_name = kcu.constraint_name AND tc.table_schema = kcu.table_schema "
                + "WHERE tc.table_schema = @schema AND tc.table_name = @table AND tc.constraint_type = 'PRIMARY KEY'",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        primaryKeys.Add(reader.GetString(0));
                    }
                }
            }

            return primaryKeys;
        }

        private async Task<List<ForeignKeyDetail>> GetForeignKeysAsync(NpgsqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<ForeignKeyDetail> foreignKeys = new List<ForeignKeyDetail>();

            using (NpgsqlCommand command = new NpgsqlCommand(
                "SELECT rc.constraint_name, kcu.column_name, ccu.table_name AS referenced_table, ccu.column_name AS referenced_column "
                + "FROM information_schema.referential_constraints rc "
                + "JOIN information_schema.key_column_usage kcu ON rc.constraint_name = kcu.constraint_name AND rc.constraint_schema = kcu.constraint_schema "
                + "JOIN information_schema.constraint_column_usage ccu ON rc.unique_constraint_name = ccu.constraint_name AND rc.unique_constraint_schema = ccu.constraint_schema "
                + "WHERE kcu.table_schema = @schema AND kcu.table_name = @table",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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

        private async Task<List<IndexDetail>> GetIndexesAsync(NpgsqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<IndexDetail> indexes = new List<IndexDetail>();

            using (NpgsqlCommand command = new NpgsqlCommand(
                "SELECT indexname, indexdef FROM pg_indexes WHERE schemaname = @schema AND tablename = @table",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string indexName = reader.GetString(0);
                        string indexDef = reader.GetString(1);

                        IndexDetail index = new IndexDetail
                        {
                            IndexName = indexName,
                            IsUnique = indexDef.Contains("UNIQUE")
                        };

                        // Parse column names from the index definition
                        int startParen = indexDef.LastIndexOf('(');
                        int endParen = indexDef.LastIndexOf(')');
                        if (startParen >= 0 && endParen > startParen)
                        {
                            string columnsPart = indexDef.Substring(startParen + 1, endParen - startParen - 1);
                            string[] columnNames = columnsPart.Split(',');
                            foreach (string col in columnNames)
                            {
                                index.Columns.Add(col.Trim());
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
