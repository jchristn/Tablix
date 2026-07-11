namespace Tablix.Server
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Named MCP client configuration document.
    /// </summary>
    public class ClientMcpConfig
    {
        /// <summary>
        /// MCP server entries keyed by server name.
        /// </summary>
        public Dictionary<string, ClientMcpServerConfig> McpServers
        {
            get { return _McpServers; }
            set { _McpServers = value ?? new Dictionary<string, ClientMcpServerConfig>(); }
        }

        private Dictionary<string, ClientMcpServerConfig> _McpServers = new Dictionary<string, ClientMcpServerConfig>();

        /// <summary>
        /// Unknown top-level client configuration properties preserved during writes.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, object> AdditionalProperties
        {
            get { return _AdditionalProperties; }
            set { _AdditionalProperties = value ?? new Dictionary<string, object>(); }
        }

        private Dictionary<string, object> _AdditionalProperties = new Dictionary<string, object>();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ClientMcpConfig()
        {
        }
    }
}
