namespace Tablix.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Tablix.Core.Enums;

    /// <summary>
    /// Configuration for a single database connection.
    /// </summary>
    public class DatabaseEntry
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier for this database entry.
        /// </summary>
        public string Id
        {
            get { return _Id; }
            set { _Id = value ?? "db_" + Guid.NewGuid().ToString().Substring(0, 8); }
        }

        /// <summary>
        /// Database type.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Hostname for network-based databases.
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Port for network-based databases.
        /// </summary>
        public int Port
        {
            get { return _Port; }
            set { _Port = Math.Clamp(value, 1, 65535); }
        }

        /// <summary>
        /// Database user.
        /// </summary>
        public string User { get; set; } = null;

        /// <summary>
        /// Database password.
        /// </summary>
        public string Password { get; set; } = null;

        /// <summary>
        /// Database name.
        /// </summary>
        public string DatabaseName { get; set; } = null;

        /// <summary>
        /// Schema name.
        /// </summary>
        public string Schema { get; set; } = "public";

        /// <summary>
        /// Filename for file-based databases (e.g. SQLite).
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
        /// Free-form user-supplied context describing the database, its tables, and relationships.
        /// </summary>
        public string Context { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = "db_" + Guid.NewGuid().ToString().Substring(0, 8);
        private int _Port = 5432;
        private List<string> _AllowedQueries = new List<string> { "SELECT" };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseEntry()
        {
        }

        #endregion
    }
}
