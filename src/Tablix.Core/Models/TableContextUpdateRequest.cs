namespace Tablix.Core.Models
{
    /// <summary>
    /// Table context update request.
    /// </summary>
    public class TableContextUpdateRequest
    {
        /// <summary>
        /// Context update mode. Supported values are <c>replace</c> and <c>append</c>.
        /// </summary>
        public string Mode { get; set; } = "replace";

        /// <summary>
        /// Context text.
        /// </summary>
        public string Context { get; set; } = null;

        /// <summary>
        /// Context source label.
        /// </summary>
        public string Source { get; set; } = "user";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TableContextUpdateRequest()
        {
        }
    }
}
