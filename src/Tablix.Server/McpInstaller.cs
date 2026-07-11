namespace Tablix.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Tablix.Core.Helpers;

    /// <summary>
    /// Auto-installs Tablix MCP configuration into supported AI client config files.
    /// </summary>
    public static class McpInstaller
    {
        #region Public-Methods

        /// <summary>
        /// Detect supported AI client config files and patch them with the Tablix MCP server entry.
        /// </summary>
        /// <param name="mcpPort">The MCP server port to advertise.</param>
        public static void Install(int mcpPort)
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string url = "http://localhost:" + mcpPort.ToString() + "/rpc";

            List<ClientConfig> clients = new List<ClientConfig>
            {
                new ClientConfig("Claude Code", new string[]
                {
                    Path.Combine(homeDir, ".claude.json"),
                    Path.Combine(homeDir, ".claude", "settings.json")
                }),
                new ClientConfig("Cursor", new string[]
                {
                    Path.Combine(homeDir, ".cursor", "mcp.json")
                }),
                new ClientConfig("Codex", new string[]
                {
                    Path.Combine(homeDir, ".codex", "config.json")
                }),
                new ClientConfig("Gemini", new string[]
                {
                    Path.Combine(homeDir, ".gemini", "settings.json")
                })
            };

            foreach (ClientConfig client in clients)
            {
                try
                {
                    PatchClient(client, url);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  Warning: failed to process " + client.Name + ": " + ex.Message);
                }
            }
        }

        #endregion

        #region Private-Methods

        private static void PatchClient(ClientConfig client, string url)
        {
            string foundPath = null;

            foreach (string candidate in client.ConfigPaths)
            {
                if (File.Exists(candidate))
                {
                    foundPath = candidate;
                    break;
                }
            }

            if (foundPath == null)
            {
                Console.WriteLine("  Skipped " + client.Name + ": config not found at " + client.ConfigPaths[0]);
                return;
            }

            string json = File.ReadAllText(foundPath);
            ClientMcpConfig config = null;
            if (!String.IsNullOrWhiteSpace(json))
            {
                config = Serializer.DeserializeJson<ClientMcpConfig>(json);
            }

            if (config == null)
                config = new ClientMcpConfig();

            config.McpServers["tablix"] = new ClientMcpServerConfig
            {
                Type = "http",
                Url = url
            };

            string output = Serializer.SerializeJson(config, true);
            File.WriteAllText(foundPath, output);

            Console.WriteLine("  Installed MCP for " + client.Name + " at " + foundPath);
        }
        #endregion
    }
}
