namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using Tablix.Core.Settings;

    /// <summary>
    /// Chat settings update request.
    /// </summary>
    public class ChatSettingsUpdate
    {
        #region Public-Members

        public bool Enabled { get; set; } = true;
        public string DefaultProviderId { get; set; } = null;
        public bool DefaultStreaming { get; set; } = true;
        public string SystemPrompt { get; set; } = null;
        public int MaxContextTables { get; set; } = 100;
        public ChatToolSettings Tools { get; set; } = null;

        public List<ModelProviderUpdate> Providers
        {
            get { return _Providers; }
            set { _Providers = value ?? new List<ModelProviderUpdate>(); }
        }

        #endregion

        #region Private-Members

        private List<ModelProviderUpdate> _Providers = new List<ModelProviderUpdate>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatSettingsUpdate()
        {
        }

        #endregion
    }
}
