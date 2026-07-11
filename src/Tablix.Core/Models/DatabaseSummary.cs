namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using Tablix.Core.Enums;
    using Tablix.Core.Settings;

    /// <summary>
    /// Redacted database entry summary safe for discovery responses.
    /// </summary>
    public class DatabaseSummary
    {
        #region Public-Members

        /// <summary>
        /// Database entry identifier.
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Database type.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Hostname for network databases.
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Port for network databases.
        /// </summary>
        public int? Port { get; set; } = null;

        /// <summary>
        /// Whether a username is configured. The username itself is not returned.
        /// </summary>
        public bool HasUser { get; set; } = false;

        /// <summary>
        /// Whether a password is configured. The password itself is not returned.
        /// </summary>
        public bool HasPassword { get; set; } = false;

        /// <summary>
        /// Database name.
        /// </summary>
        public string DatabaseName { get; set; } = null;

        /// <summary>
        /// Schema name.
        /// </summary>
        public string Schema { get; set; } = null;

        /// <summary>
        /// SQLite filename or configured file path.
        /// </summary>
        public string Filename { get; set; } = null;

        /// <summary>
        /// List of allowed SQL statement types.
        /// </summary>
        public List<string> AllowedQueries
        {
            get { return _AllowedQueries; }
            set { _AllowedQueries = value ?? new List<string>(); }
        }

        /// <summary>
        /// Free-form user-supplied context describing the database.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Whether the schema crawl completed successfully.
        /// </summary>
        public bool IsCrawled { get; set; } = false;

        /// <summary>
        /// Last crawl error, if any.
        /// </summary>
        public string CrawlError { get; set; } = null;

        #endregion

        #region Private-Members

        private List<string> _AllowedQueries = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseSummary()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a redacted summary from settings and optional crawl detail.
        /// </summary>
        /// <param name="entry">Database entry.</param>
        /// <param name="detail">Crawl detail.</param>
        /// <returns>Database summary.</returns>
        public static DatabaseSummary From(DatabaseEntry entry, DatabaseDetail detail = null)
        {
            if (entry == null) return null;

            DatabaseSummary summary = new DatabaseSummary
            {
                Id = entry.Id,
                Name = entry.Name,
                Type = entry.Type,
                Hostname = entry.Hostname,
                Port = entry.Port,
                HasUser = !string.IsNullOrEmpty(entry.User),
                HasPassword = !string.IsNullOrEmpty(entry.Password),
                DatabaseName = entry.DatabaseName,
                Schema = entry.Schema,
                Filename = entry.Filename,
                AllowedQueries = new List<string>(entry.AllowedQueries),
                Context = entry.Context
            };

            if (detail != null)
            {
                summary.IsCrawled = detail.IsCrawled;
                summary.CrawlError = detail.CrawlError;
            }

            return summary;
        }

        #endregion
    }
}
