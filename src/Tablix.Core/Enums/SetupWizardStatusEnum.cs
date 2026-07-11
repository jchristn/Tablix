namespace Tablix.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Setup wizard completion state.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SetupWizardStatusEnum
    {
        /// <summary>
        /// Setup has not started.
        /// </summary>
        NotStarted,

        /// <summary>
        /// Setup is in progress.
        /// </summary>
        InProgress,

        /// <summary>
        /// Setup completed successfully.
        /// </summary>
        Complete
    }
}
