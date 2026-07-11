namespace Tablix.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Request to generate and persist table context using a model provider.
    /// </summary>
    public class BuildTableContextRequest
    {
        #region Public-Members

        /// <summary>
        /// Model provider identifier. Empty uses chat default provider.
        /// </summary>
        public string ProviderId { get; set; } = null;

        /// <summary>
        /// User-editable instructions that influence table context generation.
        /// </summary>
        public string Prompt { get; set; } = null;

        /// <summary>
        /// Optional table identifiers. Empty generates context for every crawled table.
        /// </summary>
        public List<string> TableIds { get; set; } = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BuildTableContextRequest()
        {
        }

        #endregion
    }
}
