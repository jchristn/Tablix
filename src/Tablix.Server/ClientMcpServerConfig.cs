namespace Tablix.Server
{
    /// <summary>
    /// Named MCP server configuration entry.
    /// </summary>
    public class ClientMcpServerConfig
    {
        /// <summary>
        /// MCP transport type.
        /// </summary>
        public string Type { get; set; } = null;

        /// <summary>
        /// MCP endpoint URL.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ClientMcpServerConfig()
        {
        }
    }
}
