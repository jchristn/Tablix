namespace Tablix.Core.Settings
{
    using System;

    /// <summary>
    /// Settings that control database tools available to chat.
    /// </summary>
    public class ChatToolSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable server-side database tools for chat requests.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Allow chat to execute read-only SQL through the existing Tablix query validator.
        /// </summary>
        public bool AllowReadOnlyQueries { get; set; } = true;

        /// <summary>
        /// Allow chat to update database context when the user explicitly requests it.
        /// </summary>
        public bool AllowContextUpdates { get; set; } = true;

        /// <summary>
        /// Maximum tool loop iterations per chat request. Values are clamped from 1 to 25.
        /// </summary>
        public int MaxToolIterations
        {
            get { return _MaxToolIterations; }
            set { _MaxToolIterations = Math.Clamp(value, 1, 25); }
        }

        /// <summary>
        /// Maximum tool calls per chat request. Values are clamped from 1 to 100.
        /// </summary>
        public int MaxToolCalls
        {
            get { return _MaxToolCalls; }
            set { _MaxToolCalls = Math.Clamp(value, 1, 100); }
        }

        /// <summary>
        /// Per-tool timeout in milliseconds. Values are clamped from 1000 to 300000.
        /// </summary>
        public int ToolTimeoutMs
        {
            get { return _ToolTimeoutMs; }
            set { _ToolTimeoutMs = Math.Clamp(value, 1000, 300000); }
        }

        /// <summary>
        /// Maximum model-visible characters returned from one tool call. Values are clamped from 1000 to 100000.
        /// </summary>
        public int MaxToolOutputCharacters
        {
            get { return _MaxToolOutputCharacters; }
            set { _MaxToolOutputCharacters = Math.Clamp(value, 1000, 100000); }
        }

        #endregion

        #region Private-Members

        private int _MaxToolIterations = 8;
        private int _MaxToolCalls = 20;
        private int _ToolTimeoutMs = 30000;
        private int _MaxToolOutputCharacters = 12000;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ChatToolSettings()
        {
        }

        #endregion
    }
}
