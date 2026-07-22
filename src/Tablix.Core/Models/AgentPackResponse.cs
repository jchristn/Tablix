namespace Tablix.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// MCP-ready agent instructions for one configured database.
    /// </summary>
    public class AgentPackResponse
    {
        #region Public-Members

        /// <summary>
        /// Whether the response succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Database identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Generated timestamp.
        /// </summary>
        public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Markdown agent pack.
        /// </summary>
        public string Markdown { get; set; } = null;

        /// <summary>
        /// Short instructions suitable for a system prompt or MCP client note.
        /// </summary>
        public List<string> Instructions
        {
            get { return _Instructions; }
            set { _Instructions = value ?? new List<string>(); }
        }

        /// <summary>
        /// Useful starter questions for this database.
        /// </summary>
        public List<string> SuggestedQuestions
        {
            get { return _SuggestedQuestions; }
            set { _SuggestedQuestions = value ?? new List<string>(); }
        }

        #endregion

        #region Private-Members

        private List<string> _Instructions = new List<string>();
        private List<string> _SuggestedQuestions = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AgentPackResponse()
        {
        }

        #endregion
    }
}
