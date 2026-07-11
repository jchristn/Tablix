namespace Tablix.Server
{
    using System.Collections.Generic;
    using Tablix.Core.Settings;

    /// <summary>
    /// Legacy chat settings shape used only for one-time JSON-to-SQLite migration.
    /// </summary>
    public class LegacyChatSettings
    {
        /// <summary>
        /// Legacy configured providers.
        /// </summary>
        public List<ModelProviderSettings> Providers
        {
            get { return _Providers; }
            set { _Providers = value ?? new List<ModelProviderSettings>(); }
        }

        private List<ModelProviderSettings> _Providers = new List<ModelProviderSettings>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LegacyChatSettings()
        {
        }
    }
}
