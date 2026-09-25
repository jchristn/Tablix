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
        /// File format of the client's config files.
        /// </summary>
        public ClientConfigFormatEnum Format { get; set; } = ClientConfigFormatEnum.Json;

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
        /// <param name="format">File format of the config files.</param>
        public ClientConfig(string name, string[] configPaths, ClientConfigFormatEnum format = ClientConfigFormatEnum.Json)
        {
            Name = name;
            ConfigPaths = configPaths;
            Format = format;
        }
    }
}
