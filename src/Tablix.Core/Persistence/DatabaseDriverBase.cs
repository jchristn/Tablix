namespace Tablix.Core.Persistence
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Tablix.Core.Enums;
    using Tablix.Core.Persistence.Interfaces;

    /// <summary>
    /// Base class for Tablix product-state persistence drivers.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Persistence database type.
        /// </summary>
        public abstract TablixPersistenceDatabaseTypeEnum DatabaseType { get; }

        /// <summary>
        /// Model provider data access methods.
        /// </summary>
        public IModelProviderMethods ModelProviders { get; protected set; } = null;

        /// <summary>
        /// Configured database connection data access methods.
        /// </summary>
        public IDatabaseConnectionMethods DatabaseConnections { get; protected set; } = null;

        /// <summary>
        /// Crawled database metadata data access methods.
        /// </summary>
        public IDatabaseMetadataMethods DatabaseMetadata { get; protected set; } = null;

        /// <summary>
        /// Database-level context data access methods.
        /// </summary>
        public IDatabaseContextMethods DatabaseContexts { get; protected set; } = null;

        /// <summary>
        /// Table-level context data access methods.
        /// </summary>
        public ITableContextMethods TableContexts { get; protected set; } = null;

        /// <summary>
        /// Setup wizard state data access methods.
        /// </summary>
        public ISetupStateMethods SetupState { get; protected set; } = null;

        /// <summary>
        /// Initialize the persistence database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Close the persistence database.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        public abstract Task CloseAsync(CancellationToken token = default);

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose managed resources asynchronously.
        /// </summary>
        /// <returns>Completed value task.</returns>
        public async ValueTask DisposeAsync()
        {
            await CloseAsync(CancellationToken.None).ConfigureAwait(false);
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose managed resources.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
