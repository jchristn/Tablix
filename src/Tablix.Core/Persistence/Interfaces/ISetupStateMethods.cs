namespace Tablix.Core.Persistence.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Models;

    /// <summary>
    /// Setup wizard state persistence methods.
    /// </summary>
    public interface ISetupStateMethods
    {
        /// <summary>
        /// Read setup state.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Setup state.</returns>
        Task<SetupStateRead> ReadAsync(CancellationToken token = default);

        /// <summary>
        /// Update setup state.
        /// </summary>
        /// <param name="request">Setup update request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated setup state.</returns>
        Task<SetupStateRead> UpdateAsync(SetupStateUpdateRequest request, CancellationToken token = default);

        /// <summary>
        /// Mark setup complete.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated setup state.</returns>
        Task<SetupStateRead> CompleteAsync(CancellationToken token = default);
    }
}
