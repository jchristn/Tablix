namespace Tablix.Core.DatabaseDrivers
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using SerializableDataTables;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// SQL Server database crawler and query executor.
    /// </summary>
    public class SqlServerCrawler : IDatabaseCrawler
    {
        #region Public-Methods

        /// <inheritdoc />
        public async Task<DatabaseDetail> CrawlAsync(DatabaseEntry entry, CancellationToken token = default)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            string schema = entry.Schema ?? "dbo";

            DatabaseDetail detail = new DatabaseDetail
            {
                DatabaseId = entry.Id,
                Type = DatabaseTypeEnum.SqlServer,
                DatabaseName = entry.DatabaseName,
                Schema = schema,
                Context = entry.Context
            };

            string connectionString = BuildConnectionString(entry);

            using (SqlConnection connection = new SqlConnection(connectionString))
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

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private string BuildConnectionString(DatabaseEntry entry)
        {
            return "Server=" + entry.Hostname + "," + (entry.Port ?? 1433)
                + ";Database=" + entry.DatabaseName
                + ";User Id=" + entry.User
                + ";Password=" + entry.Password
                + ";TrustServerCertificate=True";
        }

        private async Task<List<string>> GetTableNamesAsync(SqlConnection connection, string schema, CancellationToken token)
        {
            List<string> tables = new List<string>();

            using (SqlCommand command = new SqlCommand(
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_TYPE = 'BASE TABLE'",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        tables.Add(reader.GetString(0));
                    }
                }
            }

            return tables;
        }

        private async Task<List<ColumnDetail>> GetColumnsAsync(SqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<ColumnDetail> columns = new List<ColumnDetail>();

            using (SqlCommand command = new SqlCommand(
                "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, COLUMN_DEFAULT, CHARACTER_MAXIMUM_LENGTH "
                + "FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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

        private async Task<List<string>> GetPrimaryKeysAsync(SqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<string> primaryKeys = new List<string>();

            using (SqlCommand command = new SqlCommand(
                "SELECT KCU.COLUMN_NAME "
                + "FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS TC "
                + "JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE KCU ON TC.CONSTRAINT_NAME = KCU.CONSTRAINT_NAME AND TC.TABLE_SCHEMA = KCU.TABLE_SCHEMA "
                + "WHERE TC.TABLE_SCHEMA = @schema AND TC.TABLE_NAME = @table AND TC.CONSTRAINT_TYPE = 'PRIMARY KEY'",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        primaryKeys.Add(reader.GetString(0));
                    }
                }
            }

            return primaryKeys;
        }

        private async Task<List<ForeignKeyDetail>> GetForeignKeysAsync(SqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<ForeignKeyDetail> foreignKeys = new List<ForeignKeyDetail>();

            using (SqlCommand command = new SqlCommand(
                "SELECT fk.name AS constraint_name, cp.name AS column_name, rt.name AS referenced_table, cr.name AS referenced_column "
                + "FROM sys.foreign_keys fk "
                + "JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id "
                + "JOIN sys.columns cp ON fkc.parent_object_id = cp.object_id AND fkc.parent_column_id = cp.column_id "
                + "JOIN sys.columns cr ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id "
                + "JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id "
                + "JOIN sys.tables pt ON fkc.parent_object_id = pt.object_id "
                + "JOIN sys.schemas s ON pt.schema_id = s.schema_id "
                + "WHERE s.name = @schema AND pt.name = @table",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
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

        private async Task<List<IndexDetail>> GetIndexesAsync(SqlConnection connection, string schema, string tableName, CancellationToken token)
        {
            List<IndexDetail> indexes = new List<IndexDetail>();
            Dictionary<string, IndexDetail> indexMap = new Dictionary<string, IndexDetail>();

            using (SqlCommand command = new SqlCommand(
                "SELECT i.name AS index_name, c.name AS column_name, i.is_unique "
                + "FROM sys.indexes i "
                + "JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id "
                + "JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id "
                + "JOIN sys.tables t ON i.object_id = t.object_id "
                + "JOIN sys.schemas s ON t.schema_id = s.schema_id "
                + "WHERE s.name = @schema AND t.name = @table AND i.name IS NOT NULL "
                + "ORDER BY i.name, ic.key_ordinal",
                connection))
            {
                command.Parameters.AddWithValue("@schema", schema);
                command.Parameters.AddWithValue("@table", tableName);

                using (SqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string indexName = reader.GetString(0);
                        string columnName = reader.GetString(1);
                        bool isUnique = reader.GetBoolean(2);

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
