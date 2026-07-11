namespace Tablix.Core.Persistence.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Settings;

    /// <summary>
    /// Configured database connection persistence methods.
    /// </summary>
    public interface IDatabaseConnectionMethods
    {
        /// <summary>
        /// Create a database connection.
        /// </summary>
        /// <param name="database">Database connection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created database connection.</returns>
        Task<DatabaseEntry> CreateAsync(DatabaseEntry database, CancellationToken token = default);

        /// <summary>
        /// Read a database connection by identifier.
        /// </summary>
        /// <param name="id">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database connection or null.</returns>
        Task<DatabaseEntry> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Enumerate database connections.
        /// </summary>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="skip">Records to skip.</param>
        /// <param name="filter">Optional filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database connections.</returns>
        Task<List<DatabaseEntry>> EnumerateAsync(int maxResults, int skip, string filter = null, CancellationToken token = default);

        /// <summary>
        /// Count database connections.
        /// </summary>
        /// <param name="filter">Optional filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Database count.</returns>
        Task<long> CountAsync(string filter = null, CancellationToken token = default);

        /// <summary>
        /// Update a database connection.
        /// </summary>
        /// <param name="database">Database connection.</param>
        /// <param name="preserveCredentialsWhenNull">Whether to preserve existing credentials when null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated database connection.</returns>
        Task<DatabaseEntry> UpdateAsync(DatabaseEntry database, bool preserveCredentialsWhenNull = true, CancellationToken token = default);

        /// <summary>
        /// Delete a database connection.
        /// </summary>
        /// <param name="id">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        Task<bool> DeleteAsync(string id, CancellationToken token = default);
    }
}
