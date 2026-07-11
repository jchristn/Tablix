namespace Tablix.Core.Settings
{
    using System.Collections.Generic;

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
        /// Product-state persistence database settings.
        /// </summary>
        public PersistenceDatabaseSettings Persistence
        {
            get { return _Persistence; }
            set { if (value != null) _Persistence = value; }
        }

        /// <summary>
        /// API keys for Bearer token authentication.
        /// </summary>
        public List<string> ApiKeys
        {
            get { return _ApiKeys; }
            set { _ApiKeys = value ?? new List<string>(); }
        }

        /// <summary>
        /// Chat and model provider settings.
        /// </summary>
        public ChatSettings Chat
        {
            get { return _Chat; }
            set { if (value != null) _Chat = value; }
        }

        #endregion

        #region Private-Members

        private RestSettings _Rest = new RestSettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private PersistenceDatabaseSettings _Persistence = new PersistenceDatabaseSettings();
        private ChatSettings _Chat = new ChatSettings();
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
