namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Dashboard chat options.
    /// </summary>
    public class ChatOptionsResponse
    {
        #region Public-Members

        /// <summary>
        /// Whether chat is enabled.
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
        /// Available databases.
        /// </summary>
        public List<DatabaseSummary> Databases
        {
            get { return _Databases; }
            set { _Databases = value ?? new List<DatabaseSummary>(); }
        }

        /// <summary>
        /// Redacted enabled model providers.
        /// </summary>
        public List<ModelProviderSummary> Providers
        {
            get { return _Providers; }
            set { _Providers = value ?? new List<ModelProviderSummary>(); }
        }

        #endregion

        #region Private-Members

        private List<DatabaseSummary> _Databases = new List<DatabaseSummary>();
        private List<ModelProviderSummary> _Providers = new List<ModelProviderSummary>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatOptionsResponse()
        {
        }

        #endregion
    }
}
