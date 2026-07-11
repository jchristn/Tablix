namespace Tablix.Core.Models
{
    using System;
    using Tablix.Core.Enums;

    /// <summary>
    /// First-run setup wizard state.
    /// </summary>
    public class SetupStateRead
    {
        /// <summary>
        /// Setup state identifier.
        /// </summary>
        public string Id { get; set; } = "default";

        /// <summary>
        /// Setup status.
        /// </summary>
        public SetupWizardStatusEnum Status { get; set; } = SetupWizardStatusEnum.NotStarted;

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
        /// Completion timestamp in UTC.
        /// </summary>
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Last updated timestamp in UTC.
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the setup wizard should be shown.
        /// </summary>
        public bool ShouldShowWizard { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SetupStateRead()
        {
        }
    }
}
