namespace Tablix.Core.Persistence.Interfaces
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Database-level context persistence methods.
    /// </summary>
    public interface IDatabaseContextMethods
    {
        /// <summary>
        /// Read database context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Context text.</returns>
        Task<string> ReadAsync(string databaseId, CancellationToken token = default);

        /// <summary>
        /// Upsert database context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="context">Context text.</param>
        /// <param name="mode">Update mode.</param>
        /// <param name="source">Context source.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated context.</returns>
        Task<string> UpsertAsync(string databaseId, string context, string mode = "replace", string source = "user", CancellationToken token = default);
    }
}
