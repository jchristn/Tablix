namespace Tablix.Core.DatabaseDrivers
{
    using System;
    using Tablix.Core.Enums;

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
        /// <returns>Database crawler instance.</returns>
        public static IDatabaseCrawler Create(DatabaseTypeEnum type)
        {
            return type switch
            {
                DatabaseTypeEnum.Sqlite => new SqliteCrawler(),
                DatabaseTypeEnum.Postgresql => new PostgresCrawler(),
                DatabaseTypeEnum.Mysql => new MysqlCrawler(),
                DatabaseTypeEnum.SqlServer => new SqlServerCrawler(),
                _ => throw new NotSupportedException("Database type '" + type + "' is not yet supported.")
            };
        }

        #endregion
    }
}
