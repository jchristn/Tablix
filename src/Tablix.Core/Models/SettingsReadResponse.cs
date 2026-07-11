namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using Tablix.Core.Settings;

    /// <summary>
    /// Redacted settings response for the dashboard.
    /// </summary>
    public class SettingsReadResponse
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
        public ChatSettingsRead Chat { get; set; } = null;

        /// <summary>
        /// Settings paths that are saved immediately but require restart to affect existing listeners.
        /// </summary>
        public List<string> RestartRequiredPaths
        {
            get { return _RestartRequiredPaths; }
            set { _RestartRequiredPaths = value ?? new List<string>(); }
        }

        #endregion

        #region Private-Members

        private List<string> _ApiKeys = new List<string>();
        private List<string> _RestartRequiredPaths = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SettingsReadResponse()
        {
        }

        #endregion
    }
}
