namespace Tablix.Server
{
    using System.Collections.Generic;
    using Tablix.Core.Settings;

    /// <summary>
    /// Legacy settings shape used only for one-time JSON-to-SQLite migration.
    /// </summary>
    public class LegacyTablixSettings
    {
        /// <summary>
        /// Legacy configured databases.
        /// </summary>
        public List<DatabaseEntry> Databases
        {
            get { return _Databases; }
            set { _Databases = value ?? new List<DatabaseEntry>(); }
        }

        /// <summary>
        /// Legacy chat settings.
        /// </summary>
        public LegacyChatSettings Chat
        {
            get { return _Chat; }
            set { if (value != null) _Chat = value; }
        }

        private List<DatabaseEntry> _Databases = new List<DatabaseEntry>();
        private LegacyChatSettings _Chat = new LegacyChatSettings();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LegacyTablixSettings()
        {
        }
    }
}
