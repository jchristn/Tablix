namespace Tablix.Core.Models
{
    using Tablix.Core.Enums;

    /// <summary>
    /// Setup wizard state update request.
    /// </summary>
    public class SetupStateUpdateRequest
    {
        /// <summary>
        /// Setup status.
        /// </summary>
        public SetupWizardStatusEnum Status { get; set; } = SetupWizardStatusEnum.InProgress;

        /// <summary>
        /// Current setup step.
        /// </summary>
        public string CurrentStep { get; set; } = null;

        /// <summary>
        /// Selected provider identifier.
        /// </summary>
        public string SelectedProviderId { get; set; } = null;

        /// <summary>
        /// Selected database identifier.
        /// </summary>
        public string SelectedDatabaseId { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SetupStateUpdateRequest()
        {
        }
    }
}
