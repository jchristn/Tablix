namespace Tablix.Core.Settings
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Product-state persistence database settings.
    /// </summary>
    public class PersistenceDatabaseSettings
    {
        /// <summary>
        /// Persistence database type. Defaults to SQLite.
        /// </summary>
        public TablixPersistenceDatabaseTypeEnum Type { get; set; } = TablixPersistenceDatabaseTypeEnum.Sqlite;

        /// <summary>
        /// Persistence database filename. Defaults to <c>tablix.db</c>.
        /// </summary>
        public string Filename
        {
            get { return _Filename; }
            set { _Filename = string.IsNullOrWhiteSpace(value) ? "tablix.db" : value; }
        }

        private string _Filename = "tablix.db";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public PersistenceDatabaseSettings()
        {
        }
    }
}
