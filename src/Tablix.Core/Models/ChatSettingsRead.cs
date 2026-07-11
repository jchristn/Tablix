namespace Tablix.Core.Models
{
    using System.Collections.Generic;
    using Tablix.Core.Settings;

    /// <summary>
    /// Redacted chat settings for dashboard reads.
    /// </summary>
    public class ChatSettingsRead
    {
        #region Public-Members

        /// <summary>
        /// Enable chat features.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Default provider identifier.
        /// </summary>
        public string DefaultProviderId { get; set; } = null;

        /// <summary>
        /// Default streaming preference.
        /// </summary>
        public bool DefaultStreaming { get; set; } = true;

        /// <summary>
        /// Default system prompt.
        /// </summary>
        public string SystemPrompt { get; set; } = null;

        /// <summary>
        /// Maximum number of tables included in chat context.
        /// </summary>
        public int MaxContextTables { get; set; } = 100;

        /// <summary>
        /// Tool settings.
        /// </summary>
        public ChatToolSettings Tools { get; set; } = null;

        /// <summary>
        /// Prompt-processing settings.
        /// </summary>
        public PromptProcessingSettings PromptProcessing { get; set; } = null;

        /// <summary>
        /// Redacted providers.
        /// </summary>
        public List<ModelProviderRead> Providers
        {
            get { return _Providers; }
            set { _Providers = value ?? new List<ModelProviderRead>(); }
        }

        #endregion

        #region Private-Members

        private List<ModelProviderRead> _Providers = new List<ModelProviderRead>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatSettingsRead()
        {
        }

        #endregion
    }
}
