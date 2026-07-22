namespace Tablix.Core.Models
{
    /// <summary>
    /// MCP database intelligence request.
    /// </summary>
    public class McpDatabaseIntelligenceRequest
    {
        #region Public-Members

        /// <summary>
        /// Database entry identifier.
        /// </summary>
        public string DatabaseId { get; set; } = null;

        /// <summary>
        /// Whether to include the markdown agent pack.
        /// </summary>
        public bool IncludeAgentPack { get; set; } = true;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public McpDatabaseIntelligenceRequest()
        {
        }

        #endregion
    }
}
