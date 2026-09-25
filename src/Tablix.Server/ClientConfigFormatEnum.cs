namespace Tablix.Server
{
    /// <summary>
    /// File format of an AI client config file.
    /// </summary>
    public enum ClientConfigFormatEnum
    {
        /// <summary>
        /// JSON document with a top-level mcpServers object.
        /// </summary>
        Json,

        /// <summary>
        /// TOML document with [mcp_servers.name] tables.
        /// </summary>
        Toml
    }
}
