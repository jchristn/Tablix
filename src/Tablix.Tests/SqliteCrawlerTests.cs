namespace Tablix.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;
    using Xunit;

    /// <summary>
    /// Integration tests for <see cref="SqliteCrawler"/> using the sample database.
    /// </summary>
    public class SqliteCrawlerTests : IDisposable
    {
        private readonly string _TempDbPath;
        private readonly SqliteCrawler _Crawler;

        /// <summary>
        /// Instantiate and copy the sample database to a temp location.
        /// </summary>
        public SqliteCrawlerTests()
        {
            string sourceDb = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "docker", "database.db"));

            _TempDbPath = Path.Combine(Path.GetTempPath(), "tablix_test_" + Guid.NewGuid().ToString("N") + ".db");
            File.Copy(sourceDb, _TempDbPath, true);

            _Crawler = new SqliteCrawler();
        }

        /// <summary>
        /// Clean up the temporary database file.
        /// </summary>
        public void Dispose()
        {
            // SQLite connection pooling holds file handles; clear pool then force GC
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            try
            {
                if (File.Exists(_TempDbPath)) File.Delete(_TempDbPath);
                if (File.Exists(_TempDbPath + "-wal")) File.Delete(_TempDbPath + "-wal");
                if (File.Exists(_TempDbPath + "-shm")) File.Delete(_TempDbPath + "-shm");
            }
            catch (IOException)
            {
                // Best-effort cleanup; temp files will be cleaned by OS
            }
        }

        private DatabaseEntry CreateEntry()
        {
            return new DatabaseEntry
            {
                Id = "test_sqlite",
                Type = DatabaseTypeEnum.Sqlite,
                Filename = _TempDbPath
            };
        }

        /// <summary>
        /// CrawlAsync returns a DatabaseDetail with IsCrawled set to true.
        /// </summary>
        [Fact]
        public async Task CrawlAsync_ReturnsIsCrawledTrue()
        {
            DatabaseDetail detail = await _Crawler.CrawlAsync(CreateEntry());
            Assert.True(detail.IsCrawled);
        }

        /// <summary>
        /// CrawlAsync discovers exactly 3 tables: users, orders, line_items.
        /// </summary>
        [Fact]
        public async Task CrawlAsync_DiscoversThreeTables()
        {
            DatabaseDetail detail = await _Crawler.CrawlAsync(CreateEntry());
            Assert.Equal(3, detail.Tables.Count);

            System.Collections.Generic.List<string> tableNames = detail.Tables
                .Select(t => t.TableName)
                .OrderBy(n => n)
                .ToList();

            Assert.Contains("line_items", tableNames);
            Assert.Contains("orders", tableNames);
            Assert.Contains("users", tableNames);
        }

        /// <summary>
        /// The users table has exactly 4 columns: Id, Name, Email, CreatedUtc.
        /// </summary>
        [Fact]
        public async Task CrawlAsync_UsersTable_HasFourColumns()
        {
            DatabaseDetail detail = await _Crawler.CrawlAsync(CreateEntry());
            TableDetail usersTable = detail.Tables.First(t => t.TableName == "users");

            Assert.Equal(4, usersTable.Columns.Count);

            System.Collections.Generic.List<string> columnNames = usersTable.Columns
                .Select(c => c.ColumnName)
                .OrderBy(n => n)
                .ToList();

            Assert.Contains("Id", columnNames);
            Assert.Contains("Name", columnNames);
            Assert.Contains("Email", columnNames);
            Assert.Contains("CreatedUtc", columnNames);
        }

        /// <summary>
        /// The orders table has a foreign key referencing the users table.
        /// </summary>
        [Fact]
        public async Task CrawlAsync_OrdersTable_HasForeignKeyToUsers()
        {
            DatabaseDetail detail = await _Crawler.CrawlAsync(CreateEntry());
            TableDetail ordersTable = detail.Tables.First(t => t.TableName == "orders");

            Assert.NotEmpty(ordersTable.ForeignKeys);
            Assert.Contains(ordersTable.ForeignKeys, fk => fk.ReferencedTable == "users");
        }

        /// <summary>
        /// ExecuteQueryAsync with "SELECT * FROM users" returns 5 rows.
        /// </summary>
        [Fact]
        public async Task ExecuteQueryAsync_SelectUsers_ReturnsFiveRows()
        {
            QueryResult result = await _Crawler.ExecuteQueryAsync(CreateEntry(), "SELECT * FROM users");
            Assert.True(result.Success);
            Assert.Equal(5, result.RowsReturned);
        }

        /// <summary>
        /// ExecuteQueryAsync with invalid SQL throws an exception.
        /// </summary>
        [Fact]
        public async Task ExecuteQueryAsync_InvalidSql_ThrowsException()
        {
            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await _Crawler.ExecuteQueryAsync(CreateEntry(), "NOT VALID SQL STATEMENT"));
        }

        /// <summary>
        /// TestConnectionAsync succeeds for a valid database file.
        /// </summary>
        [Fact]
        public async Task TestConnectionAsync_ValidFile_Succeeds()
        {
            await _Crawler.TestConnectionAsync(CreateEntry());
        }

        /// <summary>
        /// CrawlAsync with a nonexistent file throws (after connection opens and finds no tables).
        /// SQLite auto-creates files, so we test with a path in a nonexistent directory instead.
        /// </summary>
        [Fact]
        public async Task CrawlAsync_InvalidPath_ThrowsException()
        {
            DatabaseEntry entry = new DatabaseEntry
            {
                Id = "bad_path",
                Type = DatabaseTypeEnum.Sqlite,
                Filename = Path.Combine(Path.GetTempPath(), "nonexistent_dir_" + Guid.NewGuid().ToString("N"), "nope.db")
            };

            await Assert.ThrowsAnyAsync<Exception>(async () =>
                await _Crawler.CrawlAsync(entry));
        }
    }
}
