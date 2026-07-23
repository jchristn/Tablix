namespace Tablix.Core.Persistence.Sqlite.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.Enums;
    using Tablix.Core.Persistence.Interfaces;
    using Tablix.Core.Settings;

    /// <summary>
    /// SQLite model provider persistence methods.
    /// </summary>
    public class SqliteModelProviderMethods : IModelProviderMethods
    {
        private readonly SqliteDatabaseDriver _Driver;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">SQLite persistence driver.</param>
        public SqliteModelProviderMethods(SqliteDatabaseDriver driver)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Create a model provider.
        /// </summary>
        /// <param name="provider">Provider to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Created provider.</returns>
        public async Task<ModelProviderSettings> CreateAsync(ModelProviderSettings provider, CancellationToken token = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            ModelProviderSettings.ApplyHealthCheckDefaults(provider);

            DateTime now = DateTime.UtcNow;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO model_providers (id, name, type, endpoint, api_key, model, system_prompt, enabled, default_streaming, supports_native_tool_calls, use_native_tool_calls, supports_strict_json, tool_capability_note, temperature, top_p, max_tokens, request_timeout_ms, max_concurrent_requests, health_check_enabled, health_check_url, health_check_method, health_check_interval_ms, health_check_timeout_ms, health_check_expected_status_code, healthy_threshold, unhealthy_threshold, health_check_use_auth, created_utc, updated_utc) VALUES ($id, $name, $type, $endpoint, $api_key, $model, $system_prompt, $enabled, $default_streaming, $supports_native_tool_calls, $use_native_tool_calls, $supports_strict_json, $tool_capability_note, $temperature, $top_p, $max_tokens, $request_timeout_ms, $max_concurrent_requests, $health_check_enabled, $health_check_url, $health_check_method, $health_check_interval_ms, $health_check_timeout_ms, $health_check_expected_status_code, $healthy_threshold, $unhealthy_threshold, $health_check_use_auth, $created_utc, $updated_utc)";
                AddProviderParameters(command, provider, now, now);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return provider;
        }

        /// <summary>
        /// Read a provider by identifier.
        /// </summary>
        /// <param name="id">Provider identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider or null.</returns>
        public async Task<ModelProviderSettings> ReadAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            return await _Driver.ExecuteReadAsync(async connection =>
            {
                return await ReadProviderByIdAsync(connection, id, token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate providers.
        /// </summary>
        /// <param name="maxResults">Maximum results.</param>
        /// <param name="skip">Records to skip.</param>
        /// <param name="filter">Optional filter.</param>
        /// <param name="enabled">Optional enabled filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Providers.</returns>
        public async Task<List<ModelProviderSettings>> EnumerateAsync(int maxResults, int skip, string filter = null, bool? enabled = null, CancellationToken token = default)
        {
            int safeMax = Math.Clamp(maxResults, 1, 1000);
            int safeSkip = Math.Max(skip, 0);

            return await _Driver.ExecuteReadAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = BuildProviderSelectSql(false, filter, enabled) + " ORDER BY name, id LIMIT $limit OFFSET $offset";
                AddFilterParameters(command, filter, enabled);
                command.Parameters.AddWithValue("$limit", safeMax);
                command.Parameters.AddWithValue("$offset", safeSkip);

                List<ModelProviderSettings> providers = new List<ModelProviderSettings>();
                using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false))
                {
                    providers.Add(ReadProvider(reader));
                }

                return providers;
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Count providers.
        /// </summary>
        /// <param name="filter">Optional filter.</param>
        /// <param name="enabled">Optional enabled filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Provider count.</returns>
        public async Task<long> CountAsync(string filter = null, bool? enabled = null, CancellationToken token = default)
        {
            return await _Driver.ExecuteReadAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = BuildProviderSelectSql(true, filter, enabled);
                AddFilterParameters(command, filter, enabled);
                object result = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                return Convert.ToInt64(result);
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a provider.
        /// </summary>
        /// <param name="provider">Provider to update.</param>
        /// <param name="preserveApiKeyWhenNull">Whether to preserve the existing API key when null.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated provider.</returns>
        public async Task<ModelProviderSettings> UpdateAsync(ModelProviderSettings provider, bool preserveApiKeyWhenNull = true, CancellationToken token = default)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            ModelProviderSettings.ApplyHealthCheckDefaults(provider);

            DateTime now = DateTime.UtcNow;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                ModelProviderSettings existing = await ReadProviderByIdAsync(connection, provider.Id, token).ConfigureAwait(false);
                if (existing == null) throw new KeyNotFoundException("Provider with ID '" + provider.Id + "' not found.");
                if (preserveApiKeyWhenNull && string.IsNullOrEmpty(provider.ApiKey))
                    provider.ApiKey = existing.ApiKey;

                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "UPDATE model_providers SET name = $name, type = $type, endpoint = $endpoint, api_key = $api_key, model = $model, system_prompt = $system_prompt, enabled = $enabled, default_streaming = $default_streaming, supports_native_tool_calls = $supports_native_tool_calls, use_native_tool_calls = $use_native_tool_calls, supports_strict_json = $supports_strict_json, tool_capability_note = $tool_capability_note, temperature = $temperature, top_p = $top_p, max_tokens = $max_tokens, request_timeout_ms = $request_timeout_ms, max_concurrent_requests = $max_concurrent_requests, health_check_enabled = $health_check_enabled, health_check_url = $health_check_url, health_check_method = $health_check_method, health_check_interval_ms = $health_check_interval_ms, health_check_timeout_ms = $health_check_timeout_ms, health_check_expected_status_code = $health_check_expected_status_code, healthy_threshold = $healthy_threshold, unhealthy_threshold = $unhealthy_threshold, health_check_use_auth = $health_check_use_auth, updated_utc = $updated_utc WHERE lower(id) = lower($id)";
                AddProviderParameters(command, provider, now, now);
                int count = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                if (count == 0) throw new KeyNotFoundException("Provider with ID '" + provider.Id + "' not found.");
            }, token).ConfigureAwait(false);

            return provider;
        }

        /// <summary>
        /// Delete a provider.
        /// </summary>
        /// <param name="id">Provider identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted.</returns>
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentNullException(nameof(id));

            int count = 0;
            await _Driver.ExecuteWriteAsync(async connection =>
            {
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "DELETE FROM model_providers WHERE lower(id) = lower($id)";
                command.Parameters.AddWithValue("$id", id);
                count = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }, token).ConfigureAwait(false);

            return count > 0;
        }

        private static async Task<ModelProviderSettings> ReadProviderByIdAsync(SqliteConnection connection, string id, CancellationToken token)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM model_providers WHERE lower(id) = lower($id)";
            command.Parameters.AddWithValue("$id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
                return ReadProvider(reader);

            return null;
        }

        private static string BuildProviderSelectSql(bool count, string filter, bool? enabled)
        {
            string sql = count ? "SELECT COUNT(*) FROM model_providers" : "SELECT * FROM model_providers";
            List<string> where = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter))
                where.Add("(lower(id) LIKE lower($filter) OR lower(name) LIKE lower($filter) OR lower(model) LIKE lower($filter))");
            if (enabled.HasValue)
                where.Add("enabled = $enabled");
            if (where.Count > 0)
                sql += " WHERE " + string.Join(" AND ", where);
            return sql;
        }

        private static void AddFilterParameters(SqliteCommand command, string filter, bool? enabled)
        {
            if (!string.IsNullOrWhiteSpace(filter))
                command.Parameters.AddWithValue("$filter", "%" + filter.Trim() + "%");
            if (enabled.HasValue)
                command.Parameters.AddWithValue("$enabled", SqliteDatabaseDriver.ToInt(enabled.Value));
        }

        private static void AddProviderParameters(SqliteCommand command, ModelProviderSettings provider, DateTime createdUtc, DateTime updatedUtc)
        {
            command.Parameters.AddWithValue("$id", provider.Id);
            command.Parameters.AddWithValue("$name", (object)provider.Name ?? DBNull.Value);
            command.Parameters.AddWithValue("$type", provider.Type.ToString());
            command.Parameters.AddWithValue("$endpoint", (object)provider.Endpoint ?? DBNull.Value);
            command.Parameters.AddWithValue("$api_key", (object)provider.ApiKey ?? DBNull.Value);
            command.Parameters.AddWithValue("$model", (object)provider.Model ?? DBNull.Value);
            command.Parameters.AddWithValue("$system_prompt", (object)provider.SystemPrompt ?? DBNull.Value);
            command.Parameters.AddWithValue("$enabled", SqliteDatabaseDriver.ToInt(provider.Enabled));
            command.Parameters.AddWithValue("$default_streaming", SqliteDatabaseDriver.ToInt(provider.DefaultStreaming));
            command.Parameters.AddWithValue("$supports_native_tool_calls", SqliteDatabaseDriver.ToInt(provider.SupportsNativeToolCalls));
            command.Parameters.AddWithValue("$use_native_tool_calls", SqliteDatabaseDriver.ToInt(provider.UseNativeToolCalls));
            command.Parameters.AddWithValue("$supports_strict_json", SqliteDatabaseDriver.ToInt(provider.SupportsStrictJson));
            command.Parameters.AddWithValue("$tool_capability_note", (object)provider.ToolCapabilityNote ?? DBNull.Value);
            command.Parameters.AddWithValue("$temperature", provider.Temperature.HasValue ? (object)provider.Temperature.Value : DBNull.Value);
            command.Parameters.AddWithValue("$top_p", provider.TopP.HasValue ? (object)provider.TopP.Value : DBNull.Value);
            command.Parameters.AddWithValue("$max_tokens", provider.MaxTokens.HasValue ? (object)provider.MaxTokens.Value : DBNull.Value);
            command.Parameters.AddWithValue("$request_timeout_ms", provider.RequestTimeoutMs);
            command.Parameters.AddWithValue("$max_concurrent_requests", provider.MaxConcurrentRequests);
            command.Parameters.AddWithValue("$health_check_enabled", SqliteDatabaseDriver.ToInt(provider.HealthCheckEnabled));
            command.Parameters.AddWithValue("$health_check_url", (object)provider.HealthCheckUrl ?? DBNull.Value);
            command.Parameters.AddWithValue("$health_check_method", provider.HealthCheckMethod.ToString());
            command.Parameters.AddWithValue("$health_check_interval_ms", provider.HealthCheckIntervalMs);
            command.Parameters.AddWithValue("$health_check_timeout_ms", provider.HealthCheckTimeoutMs);
            command.Parameters.AddWithValue("$health_check_expected_status_code", provider.HealthCheckExpectedStatusCode);
            command.Parameters.AddWithValue("$healthy_threshold", provider.HealthyThreshold);
            command.Parameters.AddWithValue("$unhealthy_threshold", provider.UnhealthyThreshold);
            command.Parameters.AddWithValue("$health_check_use_auth", SqliteDatabaseDriver.ToInt(provider.HealthCheckUseAuth));
            command.Parameters.AddWithValue("$created_utc", SqliteDatabaseDriver.ToStorageDate(createdUtc));
            command.Parameters.AddWithValue("$updated_utc", SqliteDatabaseDriver.ToStorageDate(updatedUtc));
        }

        private static ModelProviderSettings ReadProvider(SqliteDataReader reader)
        {
            ModelProviderSettings provider = new ModelProviderSettings
            {
                Id = Convert.ToString(reader["id"]),
                Name = Convert.ToString(reader["name"]),
                Type = Enum.Parse<ModelProviderTypeEnum>(Convert.ToString(reader["type"])),
                Endpoint = Convert.ToString(reader["endpoint"]),
                ApiKey = reader["api_key"] == DBNull.Value ? null : Convert.ToString(reader["api_key"]),
                Model = Convert.ToString(reader["model"]),
                SystemPrompt = reader["system_prompt"] == DBNull.Value ? null : Convert.ToString(reader["system_prompt"]),
                Enabled = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["enabled"])),
                DefaultStreaming = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["default_streaming"])),
                SupportsNativeToolCalls = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["supports_native_tool_calls"])),
                UseNativeToolCalls = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["use_native_tool_calls"])),
                SupportsStrictJson = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["supports_strict_json"])),
                ToolCapabilityNote = reader["tool_capability_note"] == DBNull.Value ? null : Convert.ToString(reader["tool_capability_note"]),
                RequestTimeoutMs = Convert.ToInt32(reader["request_timeout_ms"]),
                MaxConcurrentRequests = Convert.ToInt32(reader["max_concurrent_requests"]),
                HealthCheckEnabled = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["health_check_enabled"])),
                HealthCheckUrl = reader["health_check_url"] == DBNull.Value ? null : Convert.ToString(reader["health_check_url"]),
                HealthCheckMethod = Enum.Parse<HealthCheckMethodEnum>(Convert.ToString(reader["health_check_method"]), true),
                HealthCheckIntervalMs = Convert.ToInt32(reader["health_check_interval_ms"]),
                HealthCheckTimeoutMs = Convert.ToInt32(reader["health_check_timeout_ms"]),
                HealthCheckExpectedStatusCode = Convert.ToInt32(reader["health_check_expected_status_code"]),
                HealthyThreshold = Convert.ToInt32(reader["healthy_threshold"]),
                UnhealthyThreshold = Convert.ToInt32(reader["unhealthy_threshold"]),
                HealthCheckUseAuth = SqliteDatabaseDriver.ToBool(Convert.ToInt64(reader["health_check_use_auth"]))
            };

            provider.Temperature = reader["temperature"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["temperature"]);
            provider.TopP = reader["top_p"] == DBNull.Value ? (double?)null : Convert.ToDouble(reader["top_p"]);
            provider.MaxTokens = reader["max_tokens"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["max_tokens"]);
            ModelProviderSettings.ApplyHealthCheckDefaults(provider);
            return provider;
        }
    }
}
