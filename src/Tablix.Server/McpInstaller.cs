namespace Tablix.Server
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Auto-installs Tablix MCP configuration into supported AI client config files.
    /// </summary>
    public static class McpInstaller
    {
        #region Public-Members

        /// <summary>
        /// Name of the MCP server entry written to client config files.
        /// </summary>
        public const string ServerName = "tablix";

        #endregion

        #region Private-Members

        private static readonly JsonSerializerOptions _WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Detect supported AI client config files and patch them with the Tablix MCP server entry.
        /// </summary>
        /// <param name="mcpPort">The MCP server port to advertise.</param>
        public static void Install(int mcpPort)
        {
            Install(mcpPort, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        }

        /// <summary>
        /// Detect supported AI client config files under a home directory and patch them with the Tablix MCP server entry.
        /// </summary>
        /// <param name="mcpPort">The MCP server port to advertise.</param>
        /// <param name="homeDir">Home directory containing the client config files.</param>
        public static void Install(int mcpPort, string homeDir)
        {
            if (String.IsNullOrEmpty(homeDir)) throw new ArgumentNullException(nameof(homeDir));

            string url = BuildUrl(mcpPort);

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
                    Path.Combine(homeDir, ".codex", "config.toml")
                }, ClientConfigFormatEnum.Toml),
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

        /// <summary>
        /// Build the MCP Streamable HTTP endpoint URL advertised to clients.
        /// </summary>
        /// <param name="mcpPort">The MCP server port.</param>
        /// <returns>The MCP endpoint URL.</returns>
        public static string BuildUrl(int mcpPort)
        {
            return "http://localhost:" + mcpPort.ToString() + "/mcp";
        }

        /// <summary>
        /// Add or replace the Tablix entry in a JSON client config, preserving every other property.
        /// </summary>
        /// <param name="json">Existing config file contents; may be empty.</param>
        /// <param name="url">MCP endpoint URL.</param>
        /// <returns>Updated config file contents.</returns>
        public static string PatchJson(string json, string url)
        {
            JsonObject root = null;
            if (!String.IsNullOrWhiteSpace(json))
            {
                root = JsonNode.Parse(json, null, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject;
                if (root == null) throw new InvalidDataException("config root is not a JSON object");
            }

            if (root == null)
                root = new JsonObject();

            JsonObject servers = root["mcpServers"] as JsonObject;
            if (servers == null)
            {
                servers = new JsonObject();
                root["mcpServers"] = servers;
            }

            servers[ServerName] = new JsonObject
            {
                ["type"] = "http",
                ["url"] = url
            };

            return root.ToJsonString(_WriteOptions);
        }

        /// <summary>
        /// Add or replace the Tablix table in a TOML client config, preserving every other line.
        /// </summary>
        /// <param name="toml">Existing config file contents; may be empty.</param>
        /// <param name="url">MCP endpoint URL.</param>
        /// <returns>Updated config file contents.</returns>
        public static string PatchToml(string toml, string url)
        {
            string newline = toml != null && toml.Contains("\r\n") ? "\r\n" : "\n";
            string header = "[mcp_servers." + ServerName + "]";
            string subtablePrefix = "[mcp_servers." + ServerName + ".";
            List<string> section = new List<string>
            {
                header,
                "url = \"" + url.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
            };

            List<string> lines = new List<string>(String.IsNullOrEmpty(toml)
                ? Array.Empty<string>()
                : toml.Replace("\r\n", "\n").Split('\n'));

            int start = lines.FindIndex(line => line.Trim() == header);
            if (start >= 0)
            {
                int end = start + 1;
                while (end < lines.Count)
                {
                    string trimmed = lines[end].Trim();
                    if (trimmed.StartsWith("[") && !trimmed.StartsWith(subtablePrefix)) break;
                    end++;
                }

                // Keep the blank line that separated the old table from the next one.
                while (end > start + 1 && lines[end - 1].Trim().Length == 0) end--;

                lines.RemoveRange(start, end - start);
                lines.InsertRange(start, section);
            }
            else
            {
                while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0)
                    lines.RemoveAt(lines.Count - 1);

                if (lines.Count > 0) lines.Add("");
                lines.AddRange(section);
                lines.Add("");
            }

            return String.Join(newline, lines);
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

            string contents = File.ReadAllText(foundPath);
            string output = client.Format == ClientConfigFormatEnum.Toml
                ? PatchToml(contents, url)
                : PatchJson(contents, url);

            File.WriteAllText(foundPath, output, new UTF8Encoding(false));

            Console.WriteLine("  Installed MCP for " + client.Name + " at " + foundPath);
        }

        #endregion
    }
}
