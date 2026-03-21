namespace Tablix.Server
{
    using System;
    using System.Linq;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Helpers;
    using Tablix.Core.Settings;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public class Program
    {
        private static readonly string _Header = "[Program] ";
        private static CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private static Task _ServerTask = null;

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
            _ServerTask = server.StartAsync(_TokenSource.Token);

            EventWaitHandle waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            AssemblyLoadContext.Default.Unloading += (ctx) => waitHandle.Set();
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                Console.WriteLine(_Header + "termination signal received");
                eventArgs.Cancel = true;
                waitHandle.Set();
            };

            bool waitHandleSignal = false;
            do
            {
                waitHandleSignal = waitHandle.WaitOne(1000);
            }
            while (!waitHandleSignal);

            _TokenSource.Cancel();
            Console.WriteLine(_Header + "stopping at " + DateTime.UtcNow);
        }
    }
}
