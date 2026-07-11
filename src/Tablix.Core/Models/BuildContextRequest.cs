namespace Tablix.Core.Models
{
    /// <summary>
    /// Request to generate and persist database context using a model provider.
    /// </summary>
    public class BuildContextRequest
    {
        #region Public-Members

        /// <summary>
        /// Model provider identifier. Empty uses chat default provider.
        /// </summary>
        public string ProviderId { get; set; } = null;

        /// <summary>
        /// User-editable instructions that influence context generation.
        /// </summary>
        public string Prompt { get; set; } = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BuildContextRequest()
        {
        }

        #endregion
    }
}
