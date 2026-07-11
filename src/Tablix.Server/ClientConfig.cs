namespace Tablix.Server
{
    /// <summary>
    /// Describes an AI client and its candidate config file paths.
    /// </summary>
    public class ClientConfig
    {
        /// <summary>
        /// Display name of the client.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Candidate config file paths, checked in order.
        /// </summary>
        public string[] ConfigPaths { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ClientConfig()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="name">Display name.</param>
        /// <param name="configPaths">Candidate config file paths.</param>
        public ClientConfig(string name, string[] configPaths)
        {
            Name = name;
            ConfigPaths = configPaths;
        }
    }
}
