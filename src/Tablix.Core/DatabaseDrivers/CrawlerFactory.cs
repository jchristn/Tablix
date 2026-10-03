namespace Tablix.Core.DatabaseDrivers
{
    using System;
    using Tablix.Core.Enums;
    using Tablix.Core.Observability;

    /// <summary>
    /// Factory for creating database crawlers by type.
    /// </summary>
    public static class CrawlerFactory
    {
        #region Public-Methods

        /// <summary>
        /// Create a database crawler for the specified database type.
        /// </summary>
        /// <param name="type">Database type.</param>
        /// <returns>Database crawler instance, wrapped with client spans and metrics (see <see cref="InstrumentedDatabaseCrawler"/>).</returns>
        /// <exception cref="NotSupportedException">Thrown when the database type is not supported.</exception>
        public static IDatabaseCrawler Create(DatabaseTypeEnum type)
        {
            IDatabaseCrawler crawler = type switch
            {
                DatabaseTypeEnum.Sqlite => new SqliteCrawler(),
                DatabaseTypeEnum.Postgresql => new PostgresCrawler(),
                DatabaseTypeEnum.Mysql => new MysqlCrawler(),
                DatabaseTypeEnum.SqlServer => new SqlServerCrawler(),
                _ => throw new NotSupportedException("Database type '" + type + "' is not yet supported.")
            };

            return new InstrumentedDatabaseCrawler(crawler, TablixMetrics.DbSystem(type));
        }

        #endregion
    }
}
