namespace Tablix.Core.Models
{
    using Tablix.Core.Settings;

    /// <summary>
    /// Provider connectivity test request.
    /// </summary>
    public class ProviderConnectivityTestRequest
    {
        /// <summary>
        /// Unsaved provider settings to test.
        /// </summary>
        public ModelProviderSettings Provider { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ProviderConnectivityTestRequest()
        {
        }
    }
}
