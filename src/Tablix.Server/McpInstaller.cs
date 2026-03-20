namespace Tablix.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;

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
            JsonNode root = JsonNode.Parse(json) ?? new JsonObject();

            JsonObject rootObj = root.AsObject();

            if (!rootObj.ContainsKey("mcpServers"))
            {
                rootObj["mcpServers"] = new JsonObject();
            }

            JsonObject mcpServers = rootObj["mcpServers"].AsObject();

            JsonObject tablixEntry = new JsonObject
            {
                ["type"] = "http",
                ["url"] = url
            };

            mcpServers["tablix"] = tablixEntry;

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string output = rootObj.ToJsonString(options);
            File.WriteAllText(foundPath, output);

            Console.WriteLine("  Installed MCP for " + client.Name + " at " + foundPath);
        }

        #endregion

        #region Private-Classes

        /// <summary>
        /// Describes an AI client and its candidate config file paths.
        /// </summary>
        private class ClientConfig
        {
            /// <summary>
            /// Display name of the client.
            /// </summary>
            public string Name;

            /// <summary>
            /// Candidate config file paths, checked in order.
            /// </summary>
            public string[] ConfigPaths;

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

        #endregion
    }
}
