namespace Tablix.Core.Persistence.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Models;

    /// <summary>
    /// Table-level context persistence methods.
    /// </summary>
    public interface ITableContextMethods
    {
        /// <summary>
        /// Read table context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="tableId">Table identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Table context or null.</returns>
        Task<TableContextRead> ReadAsync(string databaseId, string tableId, CancellationToken token = default);

        /// <summary>
        /// Enumerate table contexts for a database.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Table contexts.</returns>
        Task<List<TableContextRead>> EnumerateAsync(string databaseId, CancellationToken token = default);

        /// <summary>
        /// Upsert table context.
        /// </summary>
        /// <param name="databaseId">Database identifier.</param>
        /// <param name="tableId">Table identifier.</param>
        /// <param name="context">Context text.</param>
        /// <param name="mode">Update mode.</param>
        /// <param name="source">Context source.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated table context.</returns>
        Task<TableContextRead> UpsertAsync(string databaseId, string tableId, string context, string mode = "replace", string source = "user", CancellationToken token = default);
    }
}
