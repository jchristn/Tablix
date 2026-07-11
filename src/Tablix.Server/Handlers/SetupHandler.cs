namespace Tablix.Server.Handlers
{
    using System;
    using System.Threading.Tasks;
    using SwiftStack.Rest;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence;
    using ApiErrorResponse = Tablix.Core.Models.ApiErrorResponse;

    /// <summary>
    /// REST handlers for first-run setup wizard state.
    /// </summary>
    public class SetupHandler
    {
        private readonly DatabaseDriverBase _Persistence;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="persistence">Persistence driver.</param>
        public SetupHandler(DatabaseDriverBase persistence)
        {
            _Persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        }

        /// <summary>
        /// Read setup state.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Setup state.</returns>
        public async Task<object> GetSetupAsync(AppRequest req)
        {
            SetupStateRead state = await _Persistence.SetupState.ReadAsync(req.CancellationToken).ConfigureAwait(false);
            await EnrichStateAsync(state, req).ConfigureAwait(false);
            return state;
        }

        /// <summary>
        /// Update setup state.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Updated setup state.</returns>
        public async Task<object> UpdateSetupAsync(AppRequest req)
        {
            SetupStateUpdateRequest request = req.GetData<SetupStateUpdateRequest>();
            if (request == null)
            {
                req.Http.Response.StatusCode = 400;
                return new ApiErrorResponse(ApiErrorEnum.BadRequest, "Request body is required.");
            }

            SetupStateRead state = await _Persistence.SetupState.UpdateAsync(request, req.CancellationToken).ConfigureAwait(false);
            await EnrichStateAsync(state, req).ConfigureAwait(false);
            return state;
        }

        /// <summary>
        /// Mark setup complete.
        /// </summary>
        /// <param name="req">REST request.</param>
        /// <returns>Updated setup state.</returns>
        public async Task<object> CompleteSetupAsync(AppRequest req)
        {
            SetupStateRead state = await _Persistence.SetupState.CompleteAsync(req.CancellationToken).ConfigureAwait(false);
            await EnrichStateAsync(state, req).ConfigureAwait(false);
            return state;
        }

        private async Task EnrichStateAsync(SetupStateRead state, AppRequest req)
        {
            if (state == null) return;

            long enabledProviders = await _Persistence.ModelProviders.CountAsync(null, true, req.CancellationToken).ConfigureAwait(false);
            long databases = await _Persistence.DatabaseConnections.CountAsync(null, req.CancellationToken).ConfigureAwait(false);
            state.ShouldShowWizard = state.Status != SetupWizardStatusEnum.Complete || enabledProviders == 0 || databases == 0;
        }
    }
}
