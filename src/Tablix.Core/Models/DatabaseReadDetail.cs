namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Database detail response with redacted connection settings.
    /// </summary>
    public class DatabaseReadDetail : DatabaseDetail
    {
        #region Public-Members

        /// <summary>
        /// Human-readable display name.
        /// </summary>
        public string Name { get; set; } = null;

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

        #endregion

        #region Private-Members

        private List<string> _AllowedQueries = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseReadDetail()
        {
        }

        #endregion
    }
}
