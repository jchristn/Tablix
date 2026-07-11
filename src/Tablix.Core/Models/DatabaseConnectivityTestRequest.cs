namespace Tablix.Core.Models
{
    using Tablix.Core.Settings;

    /// <summary>
    /// Database connectivity test request.
    /// </summary>
    public class DatabaseConnectivityTestRequest
    {
        /// <summary>
        /// Unsaved database connection settings to test.
        /// </summary>
        public DatabaseEntry Database { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DatabaseConnectivityTestRequest()
        {
        }
    }
}
