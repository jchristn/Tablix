namespace Tablix.Core.Models
{
    /// <summary>
    /// Request to update a database context description.
    /// </summary>
    public class ContextUpdateRequest
    {
        #region Public-Members

        /// <summary>
        /// New context text, or text to append when Mode is append.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Update mode. Supported values are replace and append. Default is replace.
        /// </summary>
        public string Mode { get; set; } = "replace";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ContextUpdateRequest()
        {
        }

        #endregion
    }
}
