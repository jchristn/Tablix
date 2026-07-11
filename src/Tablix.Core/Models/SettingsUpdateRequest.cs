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

        public RestSettings Rest { get; set; } = null;
        public LoggingSettings Logging { get; set; } = null;

        public List<string> ApiKeys
        {
            get { return _ApiKeys; }
            set { _ApiKeys = value ?? new List<string>(); }
        }

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
