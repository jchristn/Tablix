namespace Tablix.Core.Persistence.Sqlite
{
    using System.Collections.Generic;
    using Tablix.Core.Persistence;

    /// <summary>
    /// SQLite schema migration statements for Tablix persistence.
    /// </summary>
    public static class SqliteSchemaQueries
    {
        /// <summary>
        /// Get schema migrations.
        /// </summary>
        /// <returns>Schema migrations.</returns>
        public static List<SchemaMigration> GetMigrations()
        {
            return new List<SchemaMigration>
            {
                new SchemaMigration
                {
                    Version = 1,
                    Description = "Initial Tablix persistence schema",
                    Statements = new List<string>
                    {
                        "CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, description TEXT NOT NULL, applied_utc TEXT NOT NULL, checksum TEXT NULL)",
                        "CREATE TABLE IF NOT EXISTS model_providers (id TEXT PRIMARY KEY, name TEXT NOT NULL, type TEXT NOT NULL, endpoint TEXT NOT NULL, api_key TEXT NULL, model TEXT NOT NULL, system_prompt TEXT NULL, enabled INTEGER NOT NULL, default_streaming INTEGER NOT NULL, supports_native_tool_calls INTEGER NOT NULL, use_native_tool_calls INTEGER NOT NULL, supports_strict_json INTEGER NOT NULL, tool_capability_note TEXT NULL, temperature REAL NULL, top_p REAL NULL, max_tokens INTEGER NULL, request_timeout_ms INTEGER NOT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_model_providers_enabled ON model_providers(enabled)",
                        "CREATE INDEX IF NOT EXISTS idx_model_providers_name ON model_providers(name)",
                        "CREATE TABLE IF NOT EXISTS database_connections (id TEXT PRIMARY KEY, name TEXT NULL, type TEXT NOT NULL, hostname TEXT NULL, port INTEGER NULL, username TEXT NULL, password TEXT NULL, database_name TEXT NULL, schema_name TEXT NULL, filename TEXT NULL, context TEXT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, last_connection_test_utc TEXT NULL, last_connection_test_success INTEGER NULL, last_connection_test_message TEXT NULL)",
                        "CREATE INDEX IF NOT EXISTS idx_database_connections_name ON database_connections(name)",
                        "CREATE INDEX IF NOT EXISTS idx_database_connections_type ON database_connections(type)",
                        "CREATE TABLE IF NOT EXISTS database_allowed_queries (database_id TEXT NOT NULL, query_operation TEXT NOT NULL, PRIMARY KEY (database_id, query_operation), FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE)",
                        "CREATE TABLE IF NOT EXISTS database_crawls (id TEXT PRIMARY KEY, database_id TEXT NOT NULL, started_utc TEXT NOT NULL, completed_utc TEXT NULL, success INTEGER NOT NULL, table_count INTEGER NOT NULL, relationship_count INTEGER NOT NULL, error TEXT NULL, FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE)",
                        "CREATE INDEX IF NOT EXISTS idx_database_crawls_database_id ON database_crawls(database_id)",
                        "CREATE TABLE IF NOT EXISTS database_tables (id TEXT PRIMARY KEY, database_id TEXT NOT NULL, schema_name TEXT NULL, table_name TEXT NOT NULL, table_type TEXT NULL, row_count INTEGER NULL, description TEXT NULL, last_crawled_utc TEXT NOT NULL, FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE, UNIQUE (database_id, schema_name, table_name))",
                        "CREATE INDEX IF NOT EXISTS idx_database_tables_database_id ON database_tables(database_id)",
                        "CREATE TABLE IF NOT EXISTS database_columns (id TEXT PRIMARY KEY, table_id TEXT NOT NULL, column_name TEXT NOT NULL, ordinal INTEGER NOT NULL, data_type TEXT NULL, is_nullable INTEGER NOT NULL, is_primary_key INTEGER NOT NULL, default_value TEXT NULL, max_length INTEGER NULL, precision_value INTEGER NULL, scale_value INTEGER NULL, FOREIGN KEY (table_id) REFERENCES database_tables(id) ON DELETE CASCADE, UNIQUE (table_id, column_name))",
                        "CREATE INDEX IF NOT EXISTS idx_database_columns_table_id ON database_columns(table_id)",
                        "CREATE TABLE IF NOT EXISTS database_indexes (id TEXT PRIMARY KEY, table_id TEXT NOT NULL, index_name TEXT NOT NULL, is_unique INTEGER NOT NULL, FOREIGN KEY (table_id) REFERENCES database_tables(id) ON DELETE CASCADE)",
                        "CREATE TABLE IF NOT EXISTS database_index_columns (index_id TEXT NOT NULL, column_name TEXT NOT NULL, ordinal INTEGER NOT NULL, PRIMARY KEY (index_id, column_name), FOREIGN KEY (index_id) REFERENCES database_indexes(id) ON DELETE CASCADE)",
                        "CREATE TABLE IF NOT EXISTS database_foreign_keys (id TEXT PRIMARY KEY, table_id TEXT NOT NULL, constraint_name TEXT NULL, column_name TEXT NOT NULL, referenced_table TEXT NOT NULL, referenced_column TEXT NOT NULL, FOREIGN KEY (table_id) REFERENCES database_tables(id) ON DELETE CASCADE)",
                        "CREATE TABLE IF NOT EXISTS context_records (id TEXT PRIMARY KEY, database_id TEXT NOT NULL, table_id TEXT NULL, scope TEXT NOT NULL, context TEXT NOT NULL, source TEXT NOT NULL, provider_id TEXT NULL, prompt TEXT NULL, created_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, FOREIGN KEY (database_id) REFERENCES database_connections(id) ON DELETE CASCADE, FOREIGN KEY (table_id) REFERENCES database_tables(id) ON DELETE CASCADE)",
                        "CREATE UNIQUE INDEX IF NOT EXISTS idx_context_records_database ON context_records(database_id, scope) WHERE table_id IS NULL",
                        "CREATE UNIQUE INDEX IF NOT EXISTS idx_context_records_table ON context_records(database_id, table_id, scope) WHERE table_id IS NOT NULL",
                        "CREATE TABLE IF NOT EXISTS context_history (id TEXT PRIMARY KEY, context_record_id TEXT NOT NULL, previous_context TEXT NULL, new_context TEXT NOT NULL, source TEXT NOT NULL, provider_id TEXT NULL, prompt TEXT NULL, created_utc TEXT NOT NULL, FOREIGN KEY (context_record_id) REFERENCES context_records(id) ON DELETE CASCADE)",
                        "CREATE TABLE IF NOT EXISTS setup_state (id TEXT PRIMARY KEY, status TEXT NOT NULL, current_step TEXT NULL, selected_provider_id TEXT NULL, selected_database_id TEXT NULL, completed_utc TEXT NULL, dismissed_utc TEXT NULL, updated_utc TEXT NOT NULL)"
                    }
                },
                new SchemaMigration
                {
                    Version = 2,
                    Description = "Add model provider concurrency limit",
                    Statements = new List<string>
                    {
                        "ALTER TABLE model_providers ADD COLUMN max_concurrent_requests INTEGER NOT NULL DEFAULT 1"
                    }
                }
            };
        }
    }
}
