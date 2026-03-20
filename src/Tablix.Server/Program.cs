namespace Tablix.Server
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Tablix.Core.Helpers;
    using Tablix.Core.Settings;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            string settingsFilename = Constants.SettingsFilename;

            for (int i = 0; i < args.Length; i++)
            {
                if (String.Equals(args[i], "--settings", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    settingsFilename = args[i + 1];
                    i++;
                }
            }

            SettingsManager settingsManager = new SettingsManager(settingsFilename);

            if (args.Contains("--install-mcp", StringComparer.OrdinalIgnoreCase))
            {
                McpInstaller.Install(settingsManager.Settings.Rest.McpPort);
                return;
            }

            TablixServer server = new TablixServer(settingsFilename);
            await server.StartAsync().ConfigureAwait(false);
        }
    }
}
