namespace Tablix.Core.Settings
{
    using System.Collections.Generic;
    using Tablix.Core.Enums;

    /// <summary>
    /// Root settings object for Tablix, serialized to/from tablix.json.
    /// </summary>
    public class TablixSettings
    {
        #region Public-Members

        /// <summary>
        /// REST and MCP server settings.
        /// </summary>
        public RestSettings Rest
        {
            get { return _Rest; }
            set { if (value != null) _Rest = value; }
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get { return _Logging; }
            set { if (value != null) _Logging = value; }
        }

        /// <summary>
        /// Configured database connections.
        /// </summary>
        public List<DatabaseEntry> Databases
        {
            get { return _Databases; }
            set { _Databases = value ?? new List<DatabaseEntry>(); }
        }

        /// <summary>
        /// API keys for Bearer token authentication.
        /// </summary>
        public List<string> ApiKeys
        {
            get { return _ApiKeys; }
            set { _ApiKeys = value ?? new List<string>(); }
        }

        #endregion

        #region Private-Members

        private RestSettings _Rest = new RestSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private List<DatabaseEntry> _Databases = new List<DatabaseEntry>
        {
            new DatabaseEntry
            {
                Id = "db_sample_sqlite",
                Name = "Sample E-Commerce",
                Type = DatabaseTypeEnum.Sqlite,
                Filename = "./database.db",
                DatabaseName = "sample",
                Schema = "main",
                AllowedQueries = new List<string> { "SELECT", "INSERT", "UPDATE", "DELETE" },
                Context = "Sample e-commerce database with three tables. The 'users' table stores customer information (Name, Email, CreatedUtc). The 'orders' table tracks purchases with a foreign key to users (UserId) and includes OrderDate, Total, and Status fields. The 'line_items' table holds individual order items with a foreign key to orders (OrderId) and includes ProductName, Quantity, and UnitPrice. Typical queries: find orders by user, calculate order totals, list products purchased."
            }
        };
        private List<string> _ApiKeys = new List<string> { "tablixadmin" };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TablixSettings()
        {
        }

        #endregion
    }
}
