namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using Tablix.Core.Settings;

    /// <summary>
    /// Settings update request from dashboard.
    /// </summary>
    public class SettingsUpdateRequest
    {
        #region Public-Members

        /// <summary>
        /// REST and MCP listener settings.
        /// </summary>
        public RestSettings Rest { get; set; } = null;

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging { get; set; } = null;

        /// <summary>
        /// Product-state persistence database settings.
        /// </summary>
        public PersistenceDatabaseSettings Persistence { get; set; } = null;

        /// <summary>
        /// API keys for dashboard and REST authentication.
        /// </summary>
        public List<string> ApiKeys
        {
            get { return _ApiKeys; }
            set { _ApiKeys = value ?? new List<string>(); }
        }

        /// <summary>
        /// Chat settings.
        /// </summary>
        public ChatSettingsUpdate Chat { get; set; } = null;

        #endregion

        #region Private-Members

        private List<string> _ApiKeys = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SettingsUpdateRequest()
        {
        }

        #endregion
    }
}
