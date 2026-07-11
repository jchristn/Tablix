namespace Tablix.Core.Persistence.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Settings;

    /// <summary>
    /// Model provider persistence methods.
    /// </summary>
    public interface IModelProviderMethods
    {
        /// <summary>
        /// Create a model provider.
        /// </summary>
        /// <param name="provider">Provider to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created provider.</returns>
        Task<ModelProviderSettings> CreateAsync(ModelProviderSettings provider, CancellationToken token = default);

        /// <summary>
        /// Read a provider by identifier.
        /// </summary>
        /// <param name="id">Provider identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider or null.</returns>
        Task<ModelProviderSettings> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate providers.
        /// </summary>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="skip">Records to skip.</param>
        /// <param name="filter">Optional filter.</param>
        /// <param name="enabled">Optional enabled filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Providers.</returns>
        Task<List<ModelProviderSettings>> EnumerateAsync(int maxResults, int skip, string filter = null, bool? enabled = null, CancellationToken token = default);

        /// <summary>
        /// Count providers.
        /// </summary>
        /// <param name="filter">Optional filter.</param>
        /// <param name="enabled">Optional enabled filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider count.</returns>
        Task<long> CountAsync(string filter = null, bool? enabled = null, CancellationToken token = default);

        /// <summary>
        /// Update a provider.
        /// </summary>
        /// <param name="provider">Provider to update.</param>
        /// <param name="preserveApiKeyWhenNull">Whether to preserve the existing API key when null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated provider.</returns>
        Task<ModelProviderSettings> UpdateAsync(ModelProviderSettings provider, bool preserveApiKeyWhenNull = true, CancellationToken token = default);

        /// <summary>
        /// Delete a provider.
        /// </summary>
        /// <param name="id">Provider identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);
    }
}
