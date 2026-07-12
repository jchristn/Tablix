namespace Tablix.Core.Persistence.Sqlite.Implementations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.Enums;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence.Interfaces;

    /// <summary>
    /// SQLite setup wizard state persistence methods.
    /// </summary>
    public class SqliteSetupStateMethods : ISetupStateMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">SQLite persistence driver.</param>
        public SqliteSetupStateMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Read setup state.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Setup state.</returns>
        public async Task<SetupStateRead> ReadAsync(CancellationToken token = default)
        {
            return await _Driver.ExecuteReadAsync(async connection =>
            {
                return await ReadStateAsync(connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update setup state.
        /// </summary>
        /// <param name="request">Setup update request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated setup state.</returns>
        public async Task<SetupStateRead> UpdateAsync(SetupStateUpdateRequest request, CancellationToken token = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            SetupStateRead updated = null;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO setup_state (id, status, current_step, selected_provider_id, selected_database_id, completed_utc, dismissed_utc, updated_utc) VALUES ('default', $status, $current_step, $selected_provider_id, $selected_database_id, $completed_utc, NULL, $updated_utc) ON CONFLICT(id) DO UPDATE SET status = excluded.status, current_step = excluded.current_step, selected_provider_id = excluded.selected_provider_id, selected_database_id = excluded.selected_database_id, completed_utc = excluded.completed_utc, updated_utc = excluded.updated_utc";
                command.Parameters.AddWithValue("$status", request.Status.ToString());
                command.Parameters.AddWithValue("$current_step", (object)request.CurrentStep ?? DBNull.Value);
                command.Parameters.AddWithValue("$selected_provider_id", (object)request.SelectedProviderId ?? DBNull.Value);
                command.Parameters.AddWithValue("$selected_database_id", (object)request.SelectedDatabaseId ?? DBNull.Value);
                command.Parameters.AddWithValue("$completed_utc", request.Status == SetupWizardStatusEnum.Complete ? (object)SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow) : DBNull.Value);
                command.Parameters.AddWithValue("$updated_utc", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                updated = await ReadStateAsync(connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return updated;
        }

        /// <summary>
        /// Mark setup complete.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated setup state.</returns>
        public async Task<SetupStateRead> CompleteAsync(CancellationToken token = default)
        {
            SetupStateRead updated = null;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                SetupStateRead existing = await ReadStateAsync(connection, token).ConfigureAwait(false);

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO setup_state (id, status, current_step, selected_provider_id, selected_database_id, completed_utc, dismissed_utc, updated_utc) VALUES ('default', 'Complete', 'complete', $selected_provider_id, $selected_database_id, $completed_utc, NULL, $updated_utc) ON CONFLICT(id) DO UPDATE SET status = excluded.status, current_step = excluded.current_step, selected_provider_id = excluded.selected_provider_id, selected_database_id = excluded.selected_database_id, completed_utc = excluded.completed_utc, updated_utc = excluded.updated_utc";
                command.Parameters.AddWithValue("$selected_provider_id", (object)existing.SelectedProviderId ?? DBNull.Value);
                command.Parameters.AddWithValue("$selected_database_id", (object)existing.SelectedDatabaseId ?? DBNull.Value);
                command.Parameters.AddWithValue("$completed_utc", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                command.Parameters.AddWithValue("$updated_utc", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                updated = await ReadStateAsync(connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return updated;
        }

        /// <summary>
        /// Dismiss setup without marking it complete.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated setup state.</returns>
        public async Task<SetupStateRead> DismissAsync(CancellationToken token = default)
        {
            SetupStateRead updated = null;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                SetupStateRead existing = await ReadStateAsync(connection, token).ConfigureAwait(false);

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO setup_state (id, status, current_step, selected_provider_id, selected_database_id, completed_utc, dismissed_utc, updated_utc) VALUES ('default', $status, $current_step, $selected_provider_id, $selected_database_id, $completed_utc, $dismissed_utc, $updated_utc) ON CONFLICT(id) DO UPDATE SET dismissed_utc = excluded.dismissed_utc, updated_utc = excluded.updated_utc";
                command.Parameters.AddWithValue("$status", existing.Status.ToString());
                command.Parameters.AddWithValue("$current_step", (object)existing.CurrentStep ?? DBNull.Value);
                command.Parameters.AddWithValue("$selected_provider_id", (object)existing.SelectedProviderId ?? DBNull.Value);
                command.Parameters.AddWithValue("$selected_database_id", (object)existing.SelectedDatabaseId ?? DBNull.Value);
                command.Parameters.AddWithValue("$completed_utc", existing.CompletedUtc.HasValue ? (object)SqliteDatabaseDriver.ToStorageDate(existing.CompletedUtc.Value) : DBNull.Value);
                command.Parameters.AddWithValue("$dismissed_utc", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                command.Parameters.AddWithValue("$updated_utc", SqliteDatabaseDriver.ToStorageDate(DateTime.UtcNow));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                updated = await ReadStateAsync(connection, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return updated;
        }

        private static async Task<SetupStateRead> ReadStateAsync(SqliteConnection connection, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM setup_state WHERE id = 'default'";
            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
                return ReadState(reader);

            return new SetupStateRead();
        }

        private static SetupStateRead ReadState(SqliteDataReader reader)
        {
            SetupWizardStatusEnum status = Enum.Parse<SetupWizardStatusEnum>(Convert.ToString(reader["status"]));
            DateTime? dismissedUtc = reader["dismissed_utc"] == DBNull.Value ? (DateTime?)null : SqliteDatabaseDriver.FromStorageDate(Convert.ToString(reader["dismissed_utc"]));
            return new SetupStateRead
            {
                Id = Convert.ToString(reader["id"]),
                Status = status,
                CurrentStep = reader["current_step"] == DBNull.Value ? null : Convert.ToString(reader["current_step"]),
                SelectedProviderId = reader["selected_provider_id"] == DBNull.Value ? null : Convert.ToString(reader["selected_provider_id"]),
                SelectedDatabaseId = reader["selected_database_id"] == DBNull.Value ? null : Convert.ToString(reader["selected_database_id"]),
                CompletedUtc = reader["completed_utc"] == DBNull.Value ? (DateTime?)null : SqliteDatabaseDriver.FromStorageDate(Convert.ToString(reader["completed_utc"])),
                DismissedUtc = dismissedUtc,
                UpdatedUtc = SqliteDatabaseDriver.FromStorageDate(Convert.ToString(reader["updated_utc"])),
                ShouldShowWizard = status != SetupWizardStatusEnum.Complete && !dismissedUtc.HasValue
            };
        }
    }
}
