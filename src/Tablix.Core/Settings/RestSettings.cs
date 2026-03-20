namespace Tablix.Core.Settings
{
    using System;

    /// <summary>
    /// REST and MCP server settings.
    /// </summary>
    public class RestSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname to listen on.
        /// </summary>
        public string Hostname
        {
            get { return _Hostname; }
            set { _Hostname = value ?? "localhost"; }
        }

        /// <summary>
        /// REST API port.
        /// </summary>
        public int Port
        {
            get { return _Port; }
            set { _Port = Math.Clamp(value, 1, 65535); }
        }

        /// <summary>
        /// Enable SSL.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// MCP server port.
        /// </summary>
        public int McpPort
        {
            get { return _McpPort; }
            set { _McpPort = Math.Clamp(value, 1, 65535); }
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 9100;
        private int _McpPort = 9102;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RestSettings()
        {
        }

        #endregion
    }
}
