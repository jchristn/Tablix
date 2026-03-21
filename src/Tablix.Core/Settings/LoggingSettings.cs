namespace Tablix.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Logging configuration settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// List of syslog servers.
        /// </summary>
        public List<SyslogServer> Servers
        {
            get { return _Servers; }
            set { _Servers = value ?? new List<SyslogServer>(); }
        }

        /// <summary>
        /// Enable console logging.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Enable file logging.
        /// </summary>
        public bool FileLogging { get; set; } = true;

        /// <summary>
        /// Log directory.
        /// </summary>
        public string LogDirectory
        {
            get { return _LogDirectory; }
            set { _LogDirectory = value ?? "./logs/"; }
        }

        /// <summary>
        /// Log filename.
        /// </summary>
        public string LogFilename
        {
            get { return _LogFilename; }
            set { _LogFilename = value ?? "tablix.log"; }
        }

        /// <summary>
        /// Minimum severity level (0 = debug, 1 = info, 2 = warn, 3 = error, 4 = alert, 5 = critical, 6 = emergency).
        /// </summary>
        public int MinimumSeverity
        {
            get { return _MinimumSeverity; }
            set { _MinimumSeverity = Math.Clamp(value, 0, 7); }
        }

        /// <summary>
        /// Enable colored console output.
        /// </summary>
        public bool EnableColors { get; set; } = true;

        #endregion

        #region Private-Members

        private List<SyslogServer> _Servers = new List<SyslogServer>();
        private string _LogDirectory = "./logs/";
        private string _LogFilename = "tablix.log";
        private int _MinimumSeverity = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoggingSettings()
        {
        }

        #endregion
    }
}
