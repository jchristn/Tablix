namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;
    using Tablix.Server;
    using Tablix.Server.Mcp;
    using Touchstone.Core;

    /// <summary>
    /// Shared Touchstone test descriptors for Tablix.
    /// </summary>
    public static class TablixSuites
    {
        /// <summary>
        /// All Tablix test suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    EnumerationSuite(),
                    QueryValidatorSuite(),
                    SettingsClampingSuite(),
                    SettingsSerializationSuite(),
                    SettingsManagerSuite(),
                    SqliteCrawlerSuite(),
                    SqliteAdvancedSuite(),
                    SchemaProjectionSuite(),
                    ModelGuardSuite(),
                    CrawlerFactorySuite(),
                    CrawlCacheSuite(),
                    McpToolBehaviorSuite(),
                    McpGuidanceSuite(),
                    DockerPackagingSuite(),
                    DashboardApiContractSuite()
                };
            }
        }

        /// <summary>
        /// Pagination model tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor EnumerationSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Enumeration",
                displayName: "Enumeration Result",
                cases: new List<TestCaseDescriptor>
                {
                    Case("Enumeration", "FirstPage", "First page returns correct slice", ct =>
                    {
                        List<string> allItems = new List<string> { "a", "b", "c", "d", "e" };
                        int maxResults = 2;
                        int skip = 0;
                        List<string> page = allItems.Skip(skip).Take(maxResults).ToList();
                        long remaining = allItems.Count - skip - page.Count;

                        EnumerationResult<string> result = new EnumerationResult<string>
                        {
                            MaxResults = maxResults,
                            Skip = skip,
                            TotalRecords = allItems.Count,
                            Objects = page,
                            RecordsRemaining = remaining,
                            EndOfResults = remaining <= 0,
                            NextSkip = remaining <= 0 ? null : (int?)(skip + page.Count)
                        };

                        Equal(2, result.Objects.Count, "Object count mismatch.");
                        Equal(3L, result.RecordsRemaining, "Records remaining mismatch.");
                        False(result.EndOfResults, "EndOfResults should be false.");
                        Equal(2, result.NextSkip.Value, "NextSkip mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("Enumeration", "MaxResultsExceedsTotal", "MaxResults exceeding total returns all", ct =>
                    {
                        List<string> allItems = new List<string> { "a", "b", "c", "d", "e" };
                        int maxResults = 10;
                        int skip = 0;
                        List<string> page = allItems.Skip(skip).Take(maxResults).ToList();
                        long remaining = allItems.Count - skip - page.Count;

                        EnumerationResult<string> result = new EnumerationResult<string>
                        {
                            MaxResults = maxResults,
                            Skip = skip,
                            TotalRecords = allItems.Count,
                            Objects = page,
                            RecordsRemaining = remaining,
                            EndOfResults = remaining <= 0,
                            NextSkip = remaining <= 0 ? null : (int?)(skip + page.Count)
                        };

                        Equal(5, result.Objects.Count, "Object count mismatch.");
                        Equal(0L, result.RecordsRemaining, "Records remaining mismatch.");
                        True(result.EndOfResults, "EndOfResults should be true.");
                        Null(result.NextSkip, "NextSkip should be null.");
                        return Task.CompletedTask;
                    }),
                    Case("Enumeration", "LastPage", "Last page returns partial results", ct =>
                    {
                        List<string> allItems = new List<string> { "a", "b", "c", "d", "e" };
                        int maxResults = 2;
                        int skip = 4;
                        List<string> page = allItems.Skip(skip).Take(maxResults).ToList();
                        long remaining = allItems.Count - skip - page.Count;

                        EnumerationResult<string> result = new EnumerationResult<string>
                        {
                            MaxResults = maxResults,
                            Skip = skip,
                            TotalRecords = allItems.Count,
                            Objects = page,
                            RecordsRemaining = remaining,
                            EndOfResults = remaining <= 0,
                            NextSkip = remaining <= 0 ? null : (int?)(skip + page.Count)
                        };

                        Equal(1, result.Objects.Count, "Object count mismatch.");
                        Equal(0L, result.RecordsRemaining, "Records remaining mismatch.");
                        True(result.EndOfResults, "EndOfResults should be true.");
                        Null(result.NextSkip, "NextSkip should be null.");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Query validator tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor QueryValidatorSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "QueryValidator",
                displayName: "Query Validator",
                cases: new List<TestCaseDescriptor>
                {
                    Case("QueryValidator", "EmptyQuery", "Empty query returns an error", ct =>
                    {
                        NotNull(QueryValidator.Validate("", new List<string> { "SELECT" }), "Expected validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "NullQuery", "Null query returns an error", ct =>
                    {
                        NotNull(QueryValidator.Validate(null, new List<string> { "SELECT" }), "Expected validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "SemicolonRejected", "Semicolon in query is rejected", ct =>
                    {
                        string result = QueryValidator.Validate("SELECT 1; SELECT 2", new List<string> { "SELECT" });
                        Contains(result, "semicolon", "Expected semicolon validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "SelectAllowed", "SELECT query is allowed", ct =>
                    {
                        Null(QueryValidator.Validate("SELECT * FROM users", new List<string> { "SELECT" }), "SELECT should be allowed.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "DeleteNotAllowed", "DELETE query is rejected", ct =>
                    {
                        string result = QueryValidator.Validate("DELETE FROM users", new List<string> { "SELECT" });
                        Contains(result, "DELETE", "Expected DELETE validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "LeadingWhitespace", "Leading whitespace is stripped", ct =>
                    {
                        Null(QueryValidator.Validate("  SELECT 1", new List<string> { "SELECT" }), "SELECT should be allowed.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "LeadingSingleLineComment", "Leading single-line comment is stripped", ct =>
                    {
                        Null(QueryValidator.Validate("-- comment\nSELECT 1", new List<string> { "SELECT" }), "SELECT should be allowed.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "LeadingBlockComment", "Leading block comment is stripped", ct =>
                    {
                        Null(QueryValidator.Validate("/* comment */ SELECT 1", new List<string> { "SELECT" }), "SELECT should be allowed.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "CaseInsensitive", "Statement matching is case-insensitive", ct =>
                    {
                        Null(QueryValidator.Validate("select * from users", new List<string> { "SELECT" }), "SELECT should be allowed.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "EmptyAllowedList", "Empty allowed list rejects all", ct =>
                    {
                        NotNull(QueryValidator.Validate("SELECT 1", new List<string>()), "Expected validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "InsertAllowed", "INSERT query is allowed when configured", ct =>
                    {
                        Null(QueryValidator.Validate("INSERT INTO users VALUES (1)", new List<string> { "INSERT" }), "INSERT should be allowed.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "CommentOnlyRejected", "Comment-only query is rejected", ct =>
                    {
                        string result = QueryValidator.Validate("-- comment only", new List<string> { "SELECT" });
                        Contains(result, "empty", "Expected empty-after-comments validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "UnclosedBlockCommentRejected", "Unclosed block comment is rejected", ct =>
                    {
                        string result = QueryValidator.Validate("/* unfinished comment", new List<string> { "SELECT" });
                        Contains(result, "empty", "Expected empty-after-comments validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "MultipleLeadingCommentsStripped", "Multiple leading comments are stripped", ct =>
                    {
                        string result = QueryValidator.Validate("-- one\n/* two */\nSELECT 1", new List<string> { "SELECT" });
                        Null(result, "SELECT should be allowed after multiple comments.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "TabAndNewlineWhitespace", "Tabs and newlines before query are stripped", ct =>
                    {
                        string result = QueryValidator.Validate("\t\r\nSELECT 1", new List<string> { "SELECT" });
                        Null(result, "SELECT should be allowed after whitespace.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "AllowedListCaseInsensitive", "Allowed query list is case-insensitive", ct =>
                    {
                        string result = QueryValidator.Validate("SELECT 1", new List<string> { "select" });
                        Null(result, "Allowed list should be case-insensitive.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "TrailingSemicolonRejected", "Trailing semicolon is rejected", ct =>
                    {
                        string result = QueryValidator.Validate("SELECT 1;", new List<string> { "SELECT" });
                        Contains(result, "semicolon", "Expected semicolon validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "SemicolonInStringRejected", "Semicolon inside string literal is conservatively rejected", ct =>
                    {
                        string result = QueryValidator.Validate("SELECT ';'", new List<string> { "SELECT" });
                        Contains(result, "semicolon", "Expected conservative semicolon rejection.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "WithClauseRejectedUnlessAllowed", "CTE leading WITH requires WITH permission", ct =>
                    {
                        string result = QueryValidator.Validate("WITH x AS (SELECT 1) SELECT * FROM x", new List<string> { "SELECT" });
                        Contains(result, "WITH", "Expected WITH validation error.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "WithClauseAllowed", "CTE leading WITH is allowed when configured", ct =>
                    {
                        string result = QueryValidator.Validate("WITH x AS (SELECT 1) SELECT * FROM x", new List<string> { "WITH" });
                        Null(result, "WITH should be allowed when configured.");
                        return Task.CompletedTask;
                    }),
                    Case("QueryValidator", "AllowedListNullRejectsAll", "Null allowed query list rejects all", ct =>
                    {
                        string result = QueryValidator.Validate("SELECT 1", null);
                        Contains(result, "No query types", "Expected null allowed-list validation error.");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Settings clamping tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SettingsClampingSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SettingsClamping",
                displayName: "Settings Clamping",
                cases: new List<TestCaseDescriptor>
                {
                    Case("SettingsClamping", "RestPortZero", "REST port zero clamps to one", ct =>
                    {
                        RestSettings rest = new RestSettings();
                        rest.Port = 0;
                        Equal(1, rest.Port, "Port mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "RestPortOverflow", "REST port overflow clamps to max", ct =>
                    {
                        RestSettings rest = new RestSettings();
                        rest.Port = 99999;
                        Equal(65535, rest.Port, "Port mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "RestPortValid", "REST port valid value is preserved", ct =>
                    {
                        RestSettings rest = new RestSettings();
                        rest.Port = 9100;
                        Equal(9100, rest.Port, "Port mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "McpPortRange", "MCP port clamps to valid range", ct =>
                    {
                        RestSettings rest = new RestSettings();
                        rest.McpPort = 0;
                        Equal(1, rest.McpPort, "MCP port low clamp mismatch.");
                        rest.McpPort = 99999;
                        Equal(65535, rest.McpPort, "MCP port high clamp mismatch.");
                        rest.McpPort = 8080;
                        Equal(8080, rest.McpPort, "MCP port valid mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "HostnameDefault", "REST hostname null defaults to localhost", ct =>
                    {
                        RestSettings rest = new RestSettings();
                        rest.Hostname = null;
                        Equal("localhost", rest.Hostname, "Hostname mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "MinimumSeverityRange", "Minimum severity clamps to range", ct =>
                    {
                        LoggingSettings logging = new LoggingSettings();
                        logging.MinimumSeverity = -1;
                        Equal(0, logging.MinimumSeverity, "Minimum severity low clamp mismatch.");
                        logging.MinimumSeverity = 10;
                        Equal(7, logging.MinimumSeverity, "Minimum severity high clamp mismatch.");
                        logging.MinimumSeverity = 3;
                        Equal(3, logging.MinimumSeverity, "Minimum severity valid mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "LogDirectoryDefault", "Log directory null defaults to logs directory", ct =>
                    {
                        LoggingSettings logging = new LoggingSettings();
                        logging.LogDirectory = null;
                        Equal("./logs/", logging.LogDirectory, "Log directory mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "DatabasePortRange", "Database port clamps to valid range and supports null", ct =>
                    {
                        DatabaseEntry entry = new DatabaseEntry();
                        Null(entry.Port, "Initial port should be null.");
                        entry.Port = 0;
                        Equal(1, entry.Port.Value, "Database port low clamp mismatch.");
                        entry.Port = 99999;
                        Equal(65535, entry.Port.Value, "Database port high clamp mismatch.");
                        entry.Port = 3306;
                        Equal(3306, entry.Port.Value, "Database port valid mismatch.");
                        entry.Port = null;
                        Null(entry.Port, "Final port should be null.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "AllowedQueriesNull", "AllowedQueries null defaults to empty list", ct =>
                    {
                        DatabaseEntry entry = new DatabaseEntry();
                        entry.AllowedQueries = null;
                        NotNull(entry.AllowedQueries, "AllowedQueries should not be null.");
                        Equal(0, entry.AllowedQueries.Count, "AllowedQueries should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "RestNullIgnored", "TablixSettings Rest null assignment is ignored", ct =>
                    {
                        TablixSettings settings = new TablixSettings();
                        settings.Rest = null;
                        NotNull(settings.Rest, "Rest should not be null.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "DatabasesNull", "TablixSettings Databases null defaults to empty list", ct =>
                    {
                        TablixSettings settings = new TablixSettings();
                        settings.Databases = null;
                        NotNull(settings.Databases, "Databases should not be null.");
                        Equal(0, settings.Databases.Count, "Databases should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "ApiKeysNull", "TablixSettings ApiKeys null defaults to empty list", ct =>
                    {
                        TablixSettings settings = new TablixSettings();
                        settings.ApiKeys = null;
                        NotNull(settings.ApiKeys, "ApiKeys should not be null.");
                        Equal(0, settings.ApiKeys.Count, "ApiKeys should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "EnumerationMaxResults", "EnumerationQuery MaxResults clamps to range", ct =>
                    {
                        EnumerationQuery query = new EnumerationQuery();
                        query.MaxResults = 0;
                        Equal(1, query.MaxResults, "MaxResults low clamp mismatch.");
                        query.MaxResults = 5000;
                        Equal(1000, query.MaxResults, "MaxResults high clamp mismatch.");
                        query.MaxResults = 50;
                        Equal(50, query.MaxResults, "MaxResults valid mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "EnumerationSkip", "EnumerationQuery Skip clamps to non-negative", ct =>
                    {
                        EnumerationQuery query = new EnumerationQuery();
                        query.Skip = -5;
                        Equal(0, query.Skip, "Skip low clamp mismatch.");
                        query.Skip = 10;
                        Equal(10, query.Skip, "Skip valid mismatch.");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Settings serialization tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SettingsSerializationSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SettingsSerialization",
                displayName: "Settings Serialization",
                cases: new List<TestCaseDescriptor>
                {
                    Case("SettingsSerialization", "RoundTripDefaultSettings", "Default settings round-trip preserves fields", ct =>
                    {
                        TablixSettings original = new TablixSettings();
                        string json = Serializer.SerializeJson(original);
                        TablixSettings restored = Serializer.DeserializeJson<TablixSettings>(json);

                        NotNull(restored, "Restored settings should not be null.");
                        Equal(original.Rest.Hostname, restored.Rest.Hostname, "Hostname mismatch.");
                        Equal(original.Rest.Port, restored.Rest.Port, "Port mismatch.");
                        Equal(original.Rest.Ssl, restored.Rest.Ssl, "SSL mismatch.");
                        Equal(original.Rest.McpPort, restored.Rest.McpPort, "MCP port mismatch.");
                        Equal(original.Logging.ConsoleLogging, restored.Logging.ConsoleLogging, "Console logging mismatch.");
                        Equal(original.Logging.FileLogging, restored.Logging.FileLogging, "File logging mismatch.");
                        Equal(original.Logging.LogDirectory, restored.Logging.LogDirectory, "Log directory mismatch.");
                        Equal(original.Logging.LogFilename, restored.Logging.LogFilename, "Log filename mismatch.");
                        Equal(original.Logging.MinimumSeverity, restored.Logging.MinimumSeverity, "Minimum severity mismatch.");
                        Equal(original.Logging.EnableColors, restored.Logging.EnableColors, "Enable colors mismatch.");
                        Equal(original.Databases.Count, restored.Databases.Count, "Database count mismatch.");
                        Equal(original.ApiKeys.Count, restored.ApiKeys.Count, "API key count mismatch.");
                        NotNull(restored.Chat, "Chat settings should not be null.");
                        Equal(original.Chat.Enabled, restored.Chat.Enabled, "Chat enabled mismatch.");
                        Equal(original.Chat.DefaultProviderId, restored.Chat.DefaultProviderId, "Default provider mismatch.");
                        Equal(original.Chat.DefaultStreaming, restored.Chat.DefaultStreaming, "Default streaming mismatch.");
                        Equal(original.Chat.MaxContextTables, restored.Chat.MaxContextTables, "Max context tables mismatch.");
                        Equal(original.Chat.Tools.MaxToolIterations, restored.Chat.Tools.MaxToolIterations, "Max tool iterations mismatch.");
                        Equal(original.Chat.Providers.Count, restored.Chat.Providers.Count, "Provider count mismatch.");
                        Equal(original.Chat.Providers[0].Type, restored.Chat.Providers[0].Type, "Provider type mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "PascalCaseProperties", "Serialization uses PascalCase property names", ct =>
                    {
                        string json = Serializer.SerializeJson(new TablixSettings());
                        Contains(json, "Rest", "Expected Rest property.");
                        Contains(json, "Logging", "Expected Logging property.");
                        Contains(json, "Chat", "Expected Chat property.");
                        Contains(json, "Providers", "Expected Providers property.");
                        Contains(json, "Databases", "Expected Databases property.");
                        Contains(json, "ApiKeys", "Expected ApiKeys property.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "DefaultChatProviders", "Default settings include supported chat provider templates", ct =>
                    {
                        TablixSettings settings = new TablixSettings();

                        NotNull(settings.Chat, "Chat settings should not be null.");
                        Contains(settings.Chat.SystemPrompt, "Restrict your conversation to only the selected database", "Default chat prompt should restrict scope.");
                        Contains(settings.Chat.SystemPrompt, "execute the query", "Default chat prompt should tell models to execute answerable data queries.");
                        Contains(settings.Chat.SystemPrompt, "instead of only describing SQL", "Default chat prompt should discourage SQL-only answers when a tool can execute.");
                        Contains(settings.Chat.SystemPrompt, "one permitted SQL statement", "Default chat prompt should give concise query tool usage guidance.");
                        Contains(settings.Chat.SystemPrompt, "bad or unknown column", "Default chat prompt should handle unknown column failures.");
                        Contains(settings.Chat.SystemPrompt, "column type mismatch", "Default chat prompt should handle column type failures.");
                        Contains(settings.Chat.SystemPrompt, "update the database context", "Default chat prompt should correct stale saved context.");
                        True(settings.Chat.Providers.Any(provider => provider.Type == ModelProviderTypeEnum.Ollama), "Expected Ollama provider.");
                        True(settings.Chat.Providers.Any(provider => provider.Type == ModelProviderTypeEnum.OpenAI), "Expected OpenAI provider.");
                        True(settings.Chat.Providers.Any(provider => provider.Type == ModelProviderTypeEnum.OpenAICompatible), "Expected OpenAI-compatible provider.");
                        True(settings.Chat.Providers.Any(provider => provider.Type == ModelProviderTypeEnum.Gemini), "Expected Gemini provider.");
                        True(settings.Chat.Providers.Any(provider => String.Equals(provider.Id, settings.Chat.DefaultProviderId, StringComparison.OrdinalIgnoreCase)), "Default provider ID should reference a configured provider.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "NullPropertiesOmitted", "Null properties are omitted from JSON", ct =>
                    {
                        DatabaseEntry entry = new DatabaseEntry();
                        entry.Hostname = null;
                        entry.User = null;
                        entry.Password = null;
                        entry.DatabaseName = null;
                        entry.Filename = null;
                        entry.Context = null;

                        string json = Serializer.SerializeJson(entry);
                        DoesNotContain(json, "\"Hostname\"", "Hostname should be omitted.");
                        DoesNotContain(json, "\"User\"", "User should be omitted.");
                        DoesNotContain(json, "\"Password\"", "Password should be omitted.");
                        DoesNotContain(json, "\"DatabaseName\"", "DatabaseName should be omitted.");
                        DoesNotContain(json, "\"Filename\"", "Filename should be omitted.");
                        DoesNotContain(json, "\"Context\"", "Context should be omitted.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "CompactJsonHasNoIndentation", "Compact serialization omits indentation", ct =>
                    {
                        string json = Serializer.SerializeJson(new DatabaseEntry { Id = "compact" }, false);
                        DoesNotContain(json, "\n", "Compact JSON should not contain newlines.");
                        DoesNotContain(json, "  ", "Compact JSON should not contain indentation.");
                        Contains(json, "\"Id\"", "Compact JSON should include Id.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "CaseInsensitiveDeserialize", "Deserializer accepts case-insensitive property names", ct =>
                    {
                        DatabaseEntry entry = Serializer.DeserializeJson<DatabaseEntry>("{\"id\":\"lower_db\",\"type\":\"Sqlite\"}");
                        Equal("lower_db", entry.Id, "Deserialized ID mismatch.");
                        Equal(DatabaseTypeEnum.Sqlite, entry.Type, "Deserialized type mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "EnumSerializesAsString", "Enums serialize as strings", ct =>
                    {
                        string json = Serializer.SerializeJson(new DatabaseEntry { Id = "enum_db", Type = DatabaseTypeEnum.Mysql });
                        Contains(json, "\"Mysql\"", "Expected enum string value.");
                        string providerJson = Serializer.SerializeJson(new ModelProviderSettings { Id = "provider_enum", Type = ModelProviderTypeEnum.OpenAICompatible });
                        Contains(providerJson, "\"OpenAICompatible\"", "Expected provider enum string value.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "SerializeNullReturnsNull", "SerializeJson null returns null", ct =>
                    {
                        Null(Serializer.SerializeJson(null), "SerializeJson null should return null.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "DeserializeEmptyThrows", "DeserializeJson empty string throws ArgumentNullException", ct =>
                    {
                        Throws<ArgumentNullException>(() => Serializer.DeserializeJson<TablixSettings>(""));
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "DeserializeInvalidJsonThrows", "DeserializeJson invalid JSON throws", ct =>
                    {
                        ThrowsAny(() => Serializer.DeserializeJson<TablixSettings>("{not json"));
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Settings manager integration tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SettingsManagerSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SettingsManager",
                displayName: "Settings Manager",
                cases: new List<TestCaseDescriptor>
                {
                    Case("SettingsManager", "CreatesDefaultSettings", "Constructor creates default settings", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            NotNull(manager.Settings, "Settings should not be null.");
                            True(File.Exists(filename), "Settings file should exist.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "AddThenGet", "AddDatabase then GetDatabase returns entry", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            manager.AddDatabase(new DatabaseEntry { Id = "test_db_1" });
                            DatabaseEntry retrieved = manager.GetDatabase("test_db_1");
                            NotNull(retrieved, "Database should be found.");
                            Equal("test_db_1", retrieved.Id, "Database ID mismatch.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "UpdateModifies", "UpdateDatabase modifies existing entry", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            DatabaseEntry entry = new DatabaseEntry { Id = "update_db", DatabaseName = "OriginalName" };
                            manager.AddDatabase(entry);
                            DatabaseEntry updated = new DatabaseEntry { Id = "update_db", DatabaseName = "UpdatedName" };
                            manager.UpdateDatabase(updated);
                            DatabaseEntry retrieved = manager.GetDatabase("update_db");
                            Equal("UpdatedName", retrieved.DatabaseName, "DatabaseName mismatch.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "DeleteRemoves", "DeleteDatabase removes entry", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            manager.AddDatabase(new DatabaseEntry { Id = "delete_db" });
                            manager.DeleteDatabase("delete_db");
                            Null(manager.GetDatabase("delete_db"), "Database should be deleted.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "DuplicateThrows", "Duplicate AddDatabase throws InvalidOperationException", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            manager.AddDatabase(new DatabaseEntry { Id = "dup_db" });
                            Throws<InvalidOperationException>(() => manager.AddDatabase(new DatabaseEntry { Id = "dup_db" }));
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "DeleteUnknownThrows", "Deleting unknown database throws KeyNotFoundException", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            Throws<KeyNotFoundException>(() => manager.DeleteDatabase("nonexistent_db"));
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "GetDatabaseCaseInsensitive", "GetDatabase is case-insensitive", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            manager.AddDatabase(new DatabaseEntry { Id = "Case_Db" });
                            NotNull(manager.GetDatabase("case_db"), "Database should be found case-insensitively.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "DeleteDatabaseCaseInsensitive", "DeleteDatabase is case-insensitive", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            manager.AddDatabase(new DatabaseEntry { Id = "Delete_Case_Db" });
                            manager.DeleteDatabase("delete_case_db");
                            Null(manager.GetDatabase("Delete_Case_Db"), "Database should be deleted case-insensitively.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "PersistenceAcrossManagers", "Saved settings are visible to a new manager", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager first = new SettingsManager(filename);
                            first.AddDatabase(new DatabaseEntry { Id = "persisted_db", DatabaseName = "Persisted" });

                            SettingsManager second = new SettingsManager(filename);
                            DatabaseEntry retrieved = second.GetDatabase("persisted_db");
                            NotNull(retrieved, "Persisted database should be found.");
                            Equal("Persisted", retrieved.DatabaseName, "Persisted database name mismatch.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "ReloadReadsDisk", "Reload reads updated settings from disk", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            TablixSettings settings = manager.Settings;
                            settings.Databases.Add(new DatabaseEntry { Id = "disk_db", DatabaseName = "Disk" });
                            File.WriteAllText(filename, Serializer.SerializeJson(settings), System.Text.Encoding.UTF8);

                            manager.Reload();
                            NotNull(manager.GetDatabase("disk_db"), "Reloaded database should be found.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "AddNullThrows", "AddDatabase null throws ArgumentNullException", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            Throws<ArgumentNullException>(() => manager.AddDatabase(null));
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "UpdateNullThrows", "UpdateDatabase null throws ArgumentNullException", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            Throws<ArgumentNullException>(() => manager.UpdateDatabase(null));
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "DeleteNullThrows", "DeleteDatabase null throws ArgumentNullException", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            Throws<ArgumentNullException>(() => manager.DeleteDatabase(null));
                        });
                        return Task.CompletedTask;
                    }),
                    Case("SettingsManager", "UpdateUnknownThrows", "UpdateDatabase unknown ID throws KeyNotFoundException", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            Throws<KeyNotFoundException>(() => manager.UpdateDatabase(new DatabaseEntry { Id = "missing_db" }));
                        });
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// SQLite crawler integration tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SqliteCrawlerSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SqliteCrawler",
                displayName: "SQLite Crawler",
                cases: new List<TestCaseDescriptor>
                {
                    Case("SqliteCrawler", "CrawlReturnsIsCrawledTrue", "CrawlAsync returns IsCrawled true", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            DatabaseDetail detail = await crawler.CrawlAsync(entry, ct).ConfigureAwait(false);
                            True(detail.IsCrawled, "IsCrawled should be true.");
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "DiscoversThreeTables", "CrawlAsync discovers three sample tables", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            DatabaseDetail detail = await crawler.CrawlAsync(entry, ct).ConfigureAwait(false);
                            Equal(3, detail.Tables.Count, "Table count mismatch.");
                            List<string> tableNames = detail.Tables.Select(t => t.TableName).OrderBy(n => n).ToList();
                            True(tableNames.Contains("line_items"), "line_items table missing.");
                            True(tableNames.Contains("orders"), "orders table missing.");
                            True(tableNames.Contains("users"), "users table missing.");
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "ReportsTableProgress", "CrawlAsync reports table and relationship progress", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            List<CrawlProgressUpdate> updates = new List<CrawlProgressUpdate>();
                            DatabaseDetail detail = await crawler.CrawlAsync(entry, async update =>
                            {
                                updates.Add(update);
                                await Task.CompletedTask.ConfigureAwait(false);
                            }, ct).ConfigureAwait(false);

                            True(detail.IsCrawled, "IsCrawled should be true.");
                            True(updates.Any(update => update.Stage == "tables_discovered" && update.TableCount == 3), "Expected tables_discovered update.");
                            Equal(3, updates.Count(update => update.Stage == "table_examined"), "Expected one table_examined update per table.");
                            True(updates.Any(update => update.Stage == "table_examined" && !String.IsNullOrEmpty(update.TableName) && update.TableIndex.HasValue), "Expected table detail in progress update.");
                            True(updates.Any(update => update.Stage == "relationships_analyzed" && update.RelationshipCount.HasValue), "Expected relationships_analyzed update.");
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "UsersHasFourColumns", "Users table has four columns", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            DatabaseDetail detail = await crawler.CrawlAsync(entry, ct).ConfigureAwait(false);
                            TableDetail usersTable = detail.Tables.First(t => t.TableName == "users");
                            Equal(4, usersTable.Columns.Count, "Column count mismatch.");
                            List<string> columnNames = usersTable.Columns.Select(c => c.ColumnName).OrderBy(n => n).ToList();
                            True(columnNames.Contains("Id"), "Id column missing.");
                            True(columnNames.Contains("Name"), "Name column missing.");
                            True(columnNames.Contains("Email"), "Email column missing.");
                            True(columnNames.Contains("CreatedUtc"), "CreatedUtc column missing.");
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "OrdersForeignKeyToUsers", "Orders table has FK to users", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            DatabaseDetail detail = await crawler.CrawlAsync(entry, ct).ConfigureAwait(false);
                            TableDetail ordersTable = detail.Tables.First(t => t.TableName == "orders");
                            True(ordersTable.ForeignKeys.Any(), "Expected at least one foreign key.");
                            True(ordersTable.ForeignKeys.Any(fk => fk.ReferencedTable == "users"), "Expected FK to users.");
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "SelectUsersReturnsFiveRows", "ExecuteQueryAsync SELECT users returns five rows", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            QueryResult result = await crawler.ExecuteQueryAsync(entry, "SELECT * FROM users", ct).ConfigureAwait(false);
                            True(result.Success, "Query should succeed.");
                            Equal(5, result.RowsReturned, "Row count mismatch.");
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "InvalidSqlThrows", "ExecuteQueryAsync invalid SQL throws", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            await ThrowsAnyAsync(async () =>
                                await crawler.ExecuteQueryAsync(entry, "NOT VALID SQL STATEMENT", ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "TestConnectionValidFile", "TestConnectionAsync succeeds for valid database", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            await crawler.TestConnectionAsync(entry, ct).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteCrawler", "InvalidPathThrows", "CrawlAsync invalid path throws", async ct =>
                    {
                        DatabaseEntry entry = new DatabaseEntry
                        {
                            Id = "bad_path",
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = Path.Combine(Path.GetTempPath(), "nonexistent_dir_" + Guid.NewGuid().ToString("N"), "nope.db")
                        };
                        SqliteCrawler crawler = new SqliteCrawler();
                        await ThrowsAnyAsync(async () => await crawler.CrawlAsync(entry, ct).ConfigureAwait(false)).ConfigureAwait(false);
                    })
                });
        }

        /// <summary>
        /// Additional SQLite crawler and query edge-case tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SqliteAdvancedSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SqliteAdvanced",
                displayName: "SQLite Advanced",
                cases: new List<TestCaseDescriptor>
                {
                    Case("SqliteAdvanced", "CrawlNullEntryThrows", "CrawlAsync null entry throws ArgumentNullException", async ct =>
                    {
                        SqliteCrawler crawler = new SqliteCrawler();
                        await ThrowsAsync<ArgumentNullException>(async () => await crawler.CrawlAsync(null, ct).ConfigureAwait(false)).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "ExecuteNullEntryThrows", "ExecuteQueryAsync null entry throws ArgumentNullException", async ct =>
                    {
                        SqliteCrawler crawler = new SqliteCrawler();
                        await ThrowsAsync<ArgumentNullException>(async () => await crawler.ExecuteQueryAsync(null, "SELECT 1", ct).ConfigureAwait(false)).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "ExecuteEmptyQueryThrows", "ExecuteQueryAsync empty query throws ArgumentNullException", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            SqliteCrawler crawler = new SqliteCrawler();
                            await ThrowsAsync<ArgumentNullException>(async () => await crawler.ExecuteQueryAsync(entry, "", ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "TestConnectionNullEntryThrows", "TestConnectionAsync null entry throws ArgumentNullException", async ct =>
                    {
                        SqliteCrawler crawler = new SqliteCrawler();
                        await ThrowsAsync<ArgumentNullException>(async () => await crawler.TestConnectionAsync(null, ct).ConfigureAwait(false)).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "FilenameRequired", "SQLite filename is required", async ct =>
                    {
                        SqliteCrawler crawler = new SqliteCrawler();
                        DatabaseEntry entry = new DatabaseEntry { Id = "missing_filename", Type = DatabaseTypeEnum.Sqlite };
                        await ThrowsAsync<ArgumentException>(async () => await crawler.CrawlAsync(entry, ct).ConfigureAwait(false)).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "DefaultAndNullabilityDiscovered", "SQLite crawl discovers defaults and nullability", async ct =>
                    {
                        await WithCustomSqliteDatabaseAsync(
                            "CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL DEFAULT 'unknown');",
                            async entry =>
                            {
                                SqliteCrawler crawler = new SqliteCrawler();
                                DatabaseDetail detail = await crawler.CrawlAsync(entry, ct).ConfigureAwait(false);
                                TableDetail parent = detail.Tables.First(t => t.TableName == "parent");
                                ColumnDetail name = parent.Columns.First(c => c.ColumnName == "Name");
                                False(name.IsNullable, "Name should be non-nullable.");
                                Equal("'unknown'", name.DefaultValue, "Default value mismatch.");
                            }).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "IndexesDiscovered", "SQLite crawl discovers index columns and uniqueness", async ct =>
                    {
                        await WithCustomSqliteDatabaseAsync(
                            "CREATE TABLE parent (Id INTEGER PRIMARY KEY, Email TEXT); CREATE UNIQUE INDEX ux_parent_email ON parent (Email);",
                            async entry =>
                            {
                                SqliteCrawler crawler = new SqliteCrawler();
                                DatabaseDetail detail = await crawler.CrawlAsync(entry, ct).ConfigureAwait(false);
                                TableDetail parent = detail.Tables.First(t => t.TableName == "parent");
                                IndexDetail index = parent.Indexes.First(i => i.IndexName == "ux_parent_email");
                                True(index.IsUnique, "Index should be unique.");
                                Equal("Email", index.Columns[0], "Index column mismatch.");
                            }).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "InternalTablesExcluded", "SQLite internal tables are excluded", async ct =>
                    {
                        await WithCustomSqliteDatabaseAsync(
                            "CREATE TABLE autoinc (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT);",
                            async entry =>
                            {
                                SqliteCrawler crawler = new SqliteCrawler();
                                DatabaseDetail detail = await crawler.CrawlAsync(entry, ct).ConfigureAwait(false);
                                False(detail.Tables.Any(t => t.TableName.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase)), "Internal sqlite tables should be excluded.");
                            }).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "EmptySelectReturnsZeroRows", "SQLite empty SELECT succeeds with zero rows", async ct =>
                    {
                        await WithCustomSqliteDatabaseAsync(
                            "CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT);",
                            async entry =>
                            {
                                SqliteCrawler crawler = new SqliteCrawler();
                                QueryResult result = await crawler.ExecuteQueryAsync(entry, "SELECT * FROM parent WHERE Id = -1", ct).ConfigureAwait(false);
                                True(result.Success, "Query should succeed.");
                                Equal(0, result.RowsReturned, "Expected zero rows.");
                                NotNull(result.Data, "Result data should not be null.");
                            }).ConfigureAwait(false);
                    }),
                    Case("SqliteAdvanced", "InsertStatementReturnsSuccess", "SQLite INSERT statement executes successfully", async ct =>
                    {
                        await WithCustomSqliteDatabaseAsync(
                            "CREATE TABLE parent (Id INTEGER PRIMARY KEY, Name TEXT);",
                            async entry =>
                            {
                                SqliteCrawler crawler = new SqliteCrawler();
                                QueryResult result = await crawler.ExecuteQueryAsync(entry, "INSERT INTO parent (Name) VALUES ('Alice')", ct).ConfigureAwait(false);
                                True(result.Success, "INSERT should succeed.");
                                Equal(0, result.RowsReturned, "INSERT should not return result rows.");
                            }).ConfigureAwait(false);
                    })
                });
        }

        /// <summary>
        /// Schema projection tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SchemaProjectionSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "SchemaProjection",
                displayName: "Schema Projection",
                cases: new List<TestCaseDescriptor>
                {
                    Case("SchemaProjection", "TableListPaginates", "Table list projection paginates and exposes NextSkip", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseTableListResult result = SchemaProjection.CreateTableListResult("sample", detail, 2, 0);
                        Equal(2, result.Objects.Count, "Page count mismatch.");
                        Equal(3L, result.TotalRecords, "TotalRecords mismatch.");
                        Equal(1L, result.RecordsRemaining, "RecordsRemaining mismatch.");
                        False(result.EndOfResults, "EndOfResults should be false.");
                        Equal(2, result.NextSkip.Value, "NextSkip mismatch.");
                        Equal("sample", result.DatabaseId, "DatabaseId mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "TableListFilters", "Table list projection filters by table name", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseTableListResult result = SchemaProjection.CreateTableListResult("sample", detail, 100, 0, "order");
                        Equal(1, result.Objects.Count, "Filtered count mismatch.");
                        Equal("orders", result.Objects[0].TableName, "Filtered table mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "RelationshipListReturnsDeclaredFk", "Relationship projection returns declared foreign keys", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseRelationshipListResult result = SchemaProjection.CreateRelationshipListResult("sample", detail, 100, 0);
                        Equal(2, result.Objects.Count, "Relationship count mismatch.");
                        True(result.Objects.Any(r => r.FromTable == "orders" && r.FromColumn == "UserId" && r.ToTable == "users"), "orders to users relationship missing.");
                        True(result.Objects.All(r => r.Source == "declared_fk"), "Unexpected relationship source.");
                        True(result.Objects.All(r => Math.Abs(r.Confidence - 1.0) < 0.0001), "Unexpected confidence.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "TableListClampsInputs", "Table list projection clamps maxResults and skip", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseTableListResult result = SchemaProjection.CreateTableListResult("sample", detail, 0, -10);
                        Equal(1, result.MaxResults, "MaxResults should clamp to one.");
                        Equal(0, result.Skip, "Skip should clamp to zero.");
                        Equal(1, result.Objects.Count, "Clamped page count mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "TableListSkipBeyondEnd", "Table list skip beyond end returns empty final page", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseTableListResult result = SchemaProjection.CreateTableListResult("sample", detail, 10, 99);
                        Equal(0, result.Objects.Count, "Objects should be empty.");
                        Equal(0L, result.RecordsRemaining, "RecordsRemaining should be zero.");
                        True(result.EndOfResults, "EndOfResults should be true.");
                        Null(result.NextSkip, "NextSkip should be null.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "TableListSchemaFilter", "Table list filters by schema", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        detail.Tables.Add(new TableDetail { SchemaName = "audit", TableName = "events" });
                        DatabaseTableListResult result = SchemaProjection.CreateTableListResult("sample", detail, 100, 0, null, "audit");
                        Equal(1, result.Objects.Count, "Schema filtered count mismatch.");
                        Equal("events", result.Objects[0].TableName, "Schema filtered table mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "RelationshipListPaginates", "Relationship projection paginates and exposes NextSkip", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseRelationshipListResult result = SchemaProjection.CreateRelationshipListResult("sample", detail, 1, 0);
                        Equal(1, result.Objects.Count, "Relationship page count mismatch.");
                        False(result.EndOfResults, "EndOfResults should be false.");
                        Equal(1, result.NextSkip.Value, "NextSkip mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "RelationshipListFilters", "Relationship projection filters by referenced table", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseRelationshipListResult result = SchemaProjection.CreateRelationshipListResult("sample", detail, 100, 0, "users");
                        Equal(1, result.Objects.Count, "Filtered relationship count mismatch.");
                        Equal("users", result.Objects[0].ToTable, "Filtered relationship target mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "RelationshipListSchemaFilter", "Relationship projection filters by schema", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        DatabaseRelationshipListResult result = SchemaProjection.CreateRelationshipListResult("sample", detail, 100, 0, null, "main");
                        Equal(2, result.Objects.Count, "Schema filtered relationship count mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "RelationshipDanglingTarget", "Relationship projection preserves dangling FK target table", ct =>
                    {
                        DatabaseDetail detail = SampleDetail();
                        detail.Tables.Add(new TableDetail
                        {
                            SchemaName = "main",
                            TableName = "dangling",
                            ForeignKeys = new List<ForeignKeyDetail>
                            {
                                new ForeignKeyDetail { ColumnName = "MissingId", ReferencedTable = "missing", ReferencedColumn = "Id" }
                            }
                        });

                        DatabaseRelationshipListResult result = SchemaProjection.CreateRelationshipListResult("sample", detail, 100, 0, "missing");
                        RelationshipDetail relationship = result.Objects.First(r => r.FromTable == "dangling");
                        Equal("missing", relationship.ToTable, "Dangling target table mismatch.");
                        Null(relationship.ToSchema, "Dangling target schema should be null.");
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "TableListNullDetailThrows", "Table list null detail throws ArgumentNullException", ct =>
                    {
                        Throws<ArgumentNullException>(() => SchemaProjection.CreateTableListResult("sample", null, 100, 0));
                        return Task.CompletedTask;
                    }),
                    Case("SchemaProjection", "RelationshipListNullDetailThrows", "Relationship list null detail throws ArgumentNullException", ct =>
                    {
                        Throws<ArgumentNullException>(() => SchemaProjection.CreateRelationshipListResult("sample", null, 100, 0));
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Model null guard and default behavior tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ModelGuardSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ModelGuards",
                displayName: "Model Guards",
                cases: new List<TestCaseDescriptor>
                {
                    Case("ModelGuards", "TableDetailNullLists", "TableDetail null lists become empty lists", ct =>
                    {
                        TableDetail table = new TableDetail();
                        table.Columns = null;
                        table.ForeignKeys = null;
                        table.Indexes = null;
                        Equal(0, table.Columns.Count, "Columns should be empty.");
                        Equal(0, table.ForeignKeys.Count, "ForeignKeys should be empty.");
                        Equal(0, table.Indexes.Count, "Indexes should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "DatabaseDetailNullTables", "DatabaseDetail null Tables becomes empty list", ct =>
                    {
                        DatabaseDetail detail = new DatabaseDetail();
                        detail.Tables = null;
                        Equal(0, detail.Tables.Count, "Tables should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "IndexDetailNullColumns", "IndexDetail null Columns becomes empty list", ct =>
                    {
                        IndexDetail index = new IndexDetail();
                        index.Columns = null;
                        Equal(0, index.Columns.Count, "Index columns should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "EnumerationResultNullObjects", "EnumerationResult null Objects becomes empty list", ct =>
                    {
                        EnumerationResult<string> result = new EnumerationResult<string>();
                        result.Objects = null;
                        Equal(0, result.Objects.Count, "Objects should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "LoggingServersNull", "LoggingSettings null Servers becomes empty list", ct =>
                    {
                        LoggingSettings settings = new LoggingSettings();
                        settings.Servers = null;
                        Equal(0, settings.Servers.Count, "Servers should be empty.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "LoggingFilenameDefault", "LoggingSettings null filename defaults", ct =>
                    {
                        LoggingSettings settings = new LoggingSettings();
                        settings.LogFilename = null;
                        Equal("tablix.log", settings.LogFilename, "Log filename default mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "TablixSettingsLoggingNullIgnored", "TablixSettings Logging null assignment is ignored", ct =>
                    {
                        TablixSettings settings = new TablixSettings();
                        settings.Logging = null;
                        NotNull(settings.Logging, "Logging should not be null.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "DatabaseEntryNullIdGeneratesId", "DatabaseEntry null Id generates a db_ ID", ct =>
                    {
                        DatabaseEntry entry = new DatabaseEntry();
                        entry.Id = null;
                        True(entry.Id.StartsWith("db_", StringComparison.OrdinalIgnoreCase), "Generated ID should start with db_.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "DatabaseSummaryRedactsCredentials", "DatabaseSummary redacts user and password values", ct =>
                    {
                        DatabaseEntry entry = new DatabaseEntry
                        {
                            Id = "secret_db",
                            Type = DatabaseTypeEnum.Postgresql,
                            Hostname = "pg.example.com",
                            User = "readonly_user",
                            Password = "plaintext-secret",
                            DatabaseName = "orders",
                            AllowedQueries = new List<string> { "SELECT" },
                            Context = "Orders database"
                        };

                        DatabaseSummary summary = DatabaseSummary.From(entry);
                        string json = Serializer.SerializeJson(summary, false);
                        True(summary.HasUser, "HasUser should indicate a configured username.");
                        True(summary.HasPassword, "HasPassword should indicate a configured password.");
                        DoesNotContain(json, "\"User\"", "Summary should not expose the User property.");
                        DoesNotContain(json, "\"Password\"", "Summary should not expose the Password property.");
                        DoesNotContain(json, "readonly_user", "Summary should not expose the username value.");
                        DoesNotContain(json, "plaintext-secret", "Summary should not expose the password value.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "DatabaseReadDetailRedactsCredentials", "DatabaseReadDetail has no user or password fields", ct =>
                    {
                        DatabaseReadDetail detail = new DatabaseReadDetail
                        {
                            DatabaseId = "secret_db",
                            HasUser = true,
                            HasPassword = true,
                            DatabaseName = "orders"
                        };

                        string json = Serializer.SerializeJson(detail, false);
                        DoesNotContain(json, "\"User\"", "Read detail should not expose the User property.");
                        DoesNotContain(json, "\"Password\"", "Read detail should not expose the Password property.");
                        Contains(json, "\"HasUser\"", "Read detail should expose HasUser.");
                        Contains(json, "\"HasPassword\"", "Read detail should expose HasPassword.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "ModelProviderSummaryRedactsApiKey", "ModelProviderSummary redacts provider API key", ct =>
                    {
                        ModelProviderSettings provider = new ModelProviderSettings
                        {
                            Id = "provider_secret",
                            Name = "Secret Provider",
                            Type = ModelProviderTypeEnum.OpenAI,
                            Endpoint = "https://api.openai.com",
                            ApiKey = "provider-secret-key",
                            Model = "gpt-4o-mini",
                            Enabled = true
                        };

                        ModelProviderSummary summary = ModelProviderSummary.From(provider);
                        string json = Serializer.SerializeJson(summary, false);
                        True(summary.HasApiKey, "HasApiKey should indicate configured key.");
                        DoesNotContain(json, "provider-secret-key", "Provider summary should not expose API key.");
                        DoesNotContain(json, "\"ApiKey\"", "Provider summary should not expose ApiKey field.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "SettingsReadProviderRedactsApiKey", "Settings provider read model omits API key value", ct =>
                    {
                        SettingsReadResponse response = new SettingsReadResponse
                        {
                            Chat = new ChatSettingsRead
                            {
                                Providers = new List<ModelProviderRead>
                                {
                                    new ModelProviderRead
                                    {
                                        Id = "provider_secret",
                                        HasApiKey = true,
                                        ApiKey = null,
                                        Model = "gpt-4o-mini"
                                    }
                                }
                            }
                        };

                        string json = Serializer.SerializeJson(response, false);
                        Contains(json, "\"HasApiKey\":true", "Settings read should expose key presence.");
                        DoesNotContain(json, "\"ApiKey\"", "Settings read should omit null ApiKey.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "ChatTelemetrySerializesRequestedFields", "Chat telemetry serializes timing and token counts", ct =>
                    {
                        ChatResponseResult result = new ChatResponseResult
                        {
                            Success = true,
                            DatabaseId = "db_sample_sqlite",
                            ProviderId = "provider_ollama_local",
                            Model = "gemma3:4b",
                            Message = "Hello",
                            Telemetry = new ChatTelemetry
                            {
                                TimeToFirstTokenMs = 10,
                                TotalStreamingTimeMs = 125,
                                InputTokens = 100,
                                OutputTokens = 20,
                                TotalTokens = 120,
                                EstimatedTokens = true
                            },
                            ToolCalls = new List<ChatToolCall>
                            {
                                new ChatToolCall
                                {
                                    Id = "tool_1",
                                    Name = "tablix_execute_query",
                                    Arguments = "{\"Query\":\"SELECT COUNT(*) FROM users\"}",
                                    Result = "{\"RowsReturned\":1}",
                                    Success = true,
                                    TotalMs = 12
                                }
                            }
                        };

                        string json = Serializer.SerializeJson(result, false);
                        Contains(json, "\"TimeToFirstTokenMs\":10", "TTFT should serialize.");
                        Contains(json, "\"TotalStreamingTimeMs\":125", "Total time should serialize.");
                        Contains(json, "\"InputTokens\":100", "Input tokens should serialize.");
                        Contains(json, "\"OutputTokens\":20", "Output tokens should serialize.");
                        Contains(json, "\"TotalTokens\":120", "Total tokens should serialize.");
                        Contains(json, "\"ToolCalls\"", "Tool calls should serialize.");
                        Contains(json, "tablix_execute_query", "Tool name should serialize.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "ChatRequestNullMessagesBecomesEmptyList", "ChatRequest null Messages becomes empty list", ct =>
                    {
                        ChatRequest request = new ChatRequest();
                        request.Messages = null;
                        Equal(0, request.Messages.Count, "Messages should become empty list.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "CrawlProgressEventSerializesTerminalDetail", "Crawl progress event serializes terminal status and final detail", ct =>
                    {
                        CrawlProgressEvent evt = new CrawlProgressEvent
                        {
                            EventType = "completed",
                            Stage = "completed",
                            DatabaseId = "db_sample_sqlite",
                            Message = "Crawl completed.",
                            Percent = 100,
                            Terminal = true,
                            TotalMs = 12.5,
                            TableCount = 3,
                            TableName = "users",
                            TableIndex = 1,
                            RelationshipCount = 2,
                            Detail = new DatabaseDetail
                            {
                                DatabaseId = "db_sample_sqlite",
                                DatabaseName = "sample",
                                IsCrawled = true,
                                Tables = new List<TableDetail>
                                {
                                    new TableDetail
                                    {
                                        TableName = "users"
                                    }
                                }
                            }
                        };

                        string json = Serializer.SerializeJson(evt, false);
                        Contains(json, "\"EventType\":\"completed\"", "Event type should serialize.");
                        Contains(json, "\"Terminal\":true", "Terminal flag should serialize.");
                        Contains(json, "\"TableCount\":3", "Table count should serialize.");
                        Contains(json, "\"TableName\":\"users\"", "Table name should serialize.");
                        Contains(json, "\"TableIndex\":1", "Table index should serialize.");
                        Contains(json, "\"RelationshipCount\":2", "Relationship count should serialize.");
                        Contains(json, "\"Detail\"", "Final detail should serialize.");
                        Contains(json, "\"Tables\"", "Final tables should serialize.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "BuildContextModelsSerialize", "Build context request and response serialize", ct =>
                    {
                        BuildContextRequest request = new BuildContextRequest
                        {
                            ProviderId = "provider_ollama_local",
                            Prompt = "Build concise context."
                        };

                        BuildContextResponse response = new BuildContextResponse
                        {
                            Success = true,
                            DatabaseId = "db_sample_sqlite",
                            ProviderId = "provider_ollama_local",
                            Context = "Generated context.",
                            Model = "gemma3:4b",
                            Telemetry = new ChatTelemetry
                            {
                                InputTokens = 10,
                                OutputTokens = 20,
                                TotalTokens = 30
                            }
                        };

                        string requestJson = Serializer.SerializeJson(request, false);
                        string responseJson = Serializer.SerializeJson(response, false);
                        Contains(requestJson, "\"ProviderId\":\"provider_ollama_local\"", "Provider ID should serialize.");
                        Contains(requestJson, "\"Prompt\":\"Build concise context.\"", "Prompt should serialize.");
                        Contains(responseJson, "\"Context\":\"Generated context.\"", "Context should serialize.");
                        Contains(responseJson, "\"Telemetry\"", "Telemetry should serialize.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "SyslogHostnameNullThrows", "SyslogServer null hostname throws", ct =>
                    {
                        SyslogServer server = new SyslogServer();
                        Throws<ArgumentNullException>(() => server.Hostname = null);
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "SyslogPortSetterRange", "SyslogServer port setter validates range", ct =>
                    {
                        SyslogServer server = new SyslogServer();
                        Throws<ArgumentOutOfRangeException>(() => server.Port = -1);
                        Throws<ArgumentOutOfRangeException>(() => server.Port = 65536);
                        server.Port = 6514;
                        Equal(6514, server.Port, "Syslog port mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("ModelGuards", "SyslogConstructorValidates", "SyslogServer constructor validates hostname and port", ct =>
                    {
                        Throws<ArgumentNullException>(() => new SyslogServer(null, 514));
                        Throws<ArgumentOutOfRangeException>(() => new SyslogServer("127.0.0.1", 65536));
                        SyslogServer server = new SyslogServer("localhost", 1514);
                        Equal("localhost", server.Hostname, "Constructor hostname mismatch.");
                        Equal(1514, server.Port, "Constructor port mismatch.");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Crawler factory tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor CrawlerFactorySuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "CrawlerFactory",
                displayName: "Crawler Factory",
                cases: new List<TestCaseDescriptor>
                {
                    Case("CrawlerFactory", "CreatesSqliteCrawler", "Factory creates SQLite crawler", ct =>
                    {
                        True(CrawlerFactory.Create(DatabaseTypeEnum.Sqlite) is SqliteCrawler, "Expected SqliteCrawler.");
                        return Task.CompletedTask;
                    }),
                    Case("CrawlerFactory", "CreatesPostgresCrawler", "Factory creates PostgreSQL crawler", ct =>
                    {
                        True(CrawlerFactory.Create(DatabaseTypeEnum.Postgresql) is PostgresCrawler, "Expected PostgresCrawler.");
                        return Task.CompletedTask;
                    }),
                    Case("CrawlerFactory", "CreatesMysqlCrawler", "Factory creates MySQL crawler", ct =>
                    {
                        True(CrawlerFactory.Create(DatabaseTypeEnum.Mysql) is MysqlCrawler, "Expected MysqlCrawler.");
                        return Task.CompletedTask;
                    }),
                    Case("CrawlerFactory", "CreatesSqlServerCrawler", "Factory creates SQL Server crawler", ct =>
                    {
                        True(CrawlerFactory.Create(DatabaseTypeEnum.SqlServer) is SqlServerCrawler, "Expected SqlServerCrawler.");
                        return Task.CompletedTask;
                    }),
                    Case("CrawlerFactory", "InvalidTypeThrows", "Factory invalid type throws NotSupportedException", ct =>
                    {
                        Throws<NotSupportedException>(() => CrawlerFactory.Create((DatabaseTypeEnum)999));
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Crawl cache behavior tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor CrawlCacheSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "CrawlCache",
                displayName: "Crawl Cache",
                cases: new List<TestCaseDescriptor>
                {
                    Case("CrawlCache", "CrawlAllNullNoops", "CrawlAllAsync null input is a no-op", async ct =>
                    {
                        CrawlCache cache = new CrawlCache();
                        await cache.CrawlAllAsync(null).ConfigureAwait(false);
                        Equal(0, cache.GetAll().Count, "Cache should remain empty.");
                    }),
                    Case("CrawlCache", "GetEmptyIdReturnsNull", "Get with empty ID returns null", ct =>
                    {
                        CrawlCache cache = new CrawlCache();
                        Null(cache.Get(""), "Empty ID should return null.");
                        Null(cache.Get(null), "Null ID should return null.");
                        return Task.CompletedTask;
                    }),
                    Case("CrawlCache", "CrawlOneNullThrows", "CrawlOneAsync null entry throws ArgumentNullException", async ct =>
                    {
                        CrawlCache cache = new CrawlCache();
                        await ThrowsAsync<ArgumentNullException>(async () => await cache.CrawlOneAsync(null).ConfigureAwait(false)).ConfigureAwait(false);
                    }),
                    Case("CrawlCache", "FailedCrawlCreatesDegradedCacheEntry", "Failed crawl creates degraded cache entry", async ct =>
                    {
                        CrawlCache cache = new CrawlCache();
                        DatabaseEntry entry = new DatabaseEntry
                        {
                            Id = "bad_cache_db",
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = Path.Combine(Path.GetTempPath(), "missing_dir_" + Guid.NewGuid().ToString("N"), "missing.db")
                        };

                        DatabaseDetail detail = await cache.CrawlOneAsync(entry).ConfigureAwait(false);
                        False(detail.IsCrawled, "Detail should be degraded.");
                        NotNull(detail.CrawlError, "CrawlError should be populated.");
                        False(cache.Get("bad_cache_db").IsCrawled, "Cached detail should be degraded.");
                    }),
                    Case("CrawlCache", "SuccessfulCrawlCanBeRemoved", "Successful crawl is cached and removable", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            CrawlCache cache = new CrawlCache();
                            DatabaseDetail detail = await cache.CrawlOneAsync(entry).ConfigureAwait(false);
                            True(detail.IsCrawled, "Detail should be crawled.");
                            NotNull(cache.Get(entry.Id), "Cached detail should exist.");
                            True(cache.GetAll().Any(d => d.DatabaseId == entry.Id), "GetAll should include cached detail.");
                            cache.Remove(entry.Id);
                            Null(cache.Get(entry.Id), "Cached detail should be removed.");
                        }).ConfigureAwait(false);
                    })
                });
        }

        /// <summary>
        /// MCP tool behavior tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor McpToolBehaviorSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "McpToolBehavior",
                displayName: "MCP Tool Behavior",
                cases: new List<TestCaseDescriptor>
                {
                    Case("McpToolBehavior", "RegistersExpectedTools", "MCP registers all expected tools", async ct =>
                    {
                        await WithTempSettingsManagerAsync(async manager =>
                        {
                            Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                            Equal(7, tools.Count, "Tool count mismatch.");
                            True(tools.ContainsKey("tablix_discover_databases"), "discover databases missing.");
                            True(tools.ContainsKey("tablix_discover_database"), "discover database missing.");
                            True(tools.ContainsKey("tablix_list_tables"), "list tables missing.");
                            True(tools.ContainsKey("tablix_discover_table"), "discover table missing.");
                            True(tools.ContainsKey("tablix_list_relationships"), "list relationships missing.");
                            True(tools.ContainsKey("tablix_execute_query"), "execute query missing.");
                            True(tools.ContainsKey("tablix_update_context"), "update context missing.");
                            await Task.CompletedTask.ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "DiscoverDatabasesPaginates", "MCP discover databases paginates", async ct =>
                    {
                        await WithTempSettingsManagerAsync(async manager =>
                        {
                            manager.AddDatabase(new DatabaseEntry { Id = "alpha_db", Name = "Alpha" });
                            manager.AddDatabase(new DatabaseEntry { Id = "beta_db", Name = "Beta" });
                            Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                            JsonElement result = ToJsonElement(await tools["tablix_discover_databases"](ParseArgs("{\"maxResults\":2,\"skip\":0}")).ConfigureAwait(false));
                            Equal(2, result.GetProperty("Objects").GetArrayLength(), "Page count mismatch.");
                            False(result.GetProperty("EndOfResults").GetBoolean(), "EndOfResults should be false.");
                            Equal(2, result.GetProperty("NextSkip").GetInt32(), "NextSkip mismatch.");
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "DiscoverDatabasesRedactsCredentials", "MCP discover databases never returns credentials", async ct =>
                    {
                        await WithTempSettingsManagerAsync(async manager =>
                        {
                            manager.AddDatabase(new DatabaseEntry
                            {
                                Id = "secret_db",
                                Type = DatabaseTypeEnum.Postgresql,
                                Hostname = "pg.example.com",
                                User = "readonly_user",
                                Password = "plaintext-secret",
                                DatabaseName = "orders"
                            });

                            Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                            object toolResult = await tools["tablix_discover_databases"](ParseArgs("{\"filter\":\"secret_db\"}")).ConfigureAwait(false);
                            string json = Serializer.SerializeJson(toolResult, false);
                            JsonElement result = ToJsonElement(toolResult);
                            JsonElement first = result.GetProperty("Objects")[0];
                            True(first.GetProperty("HasUser").GetBoolean(), "HasUser should be returned.");
                            True(first.GetProperty("HasPassword").GetBoolean(), "HasPassword should be returned.");
                            DoesNotContain(json, "\"User\"", "MCP discovery should not expose the User property.");
                            DoesNotContain(json, "\"Password\"", "MCP discovery should not expose the Password property.");
                            DoesNotContain(json, "readonly_user", "MCP discovery should not expose the username value.");
                            DoesNotContain(json, "plaintext-secret", "MCP discovery should not expose the password value.");
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "ListTablesPaginatesSample", "MCP list tables paginates sample database", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            await WithTempSettingsManagerAsync(async manager =>
                            {
                                ConfigureOnlyDatabase(manager, entry);
                                Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                                JsonElement result = ToJsonElement(await tools["tablix_list_tables"](ParseArgs("{\"databaseId\":\"test_sqlite\",\"maxResults\":2,\"skip\":0}")).ConfigureAwait(false));
                                Equal(2, result.GetProperty("Objects").GetArrayLength(), "Table page count mismatch.");
                                Equal(3, result.GetProperty("TableCount").GetInt32(), "TableCount mismatch.");
                                False(result.GetProperty("EndOfResults").GetBoolean(), "EndOfResults should be false.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "DiscoverTableReturnsGeometry", "MCP discover table returns table geometry", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            await WithTempSettingsManagerAsync(async manager =>
                            {
                                ConfigureOnlyDatabase(manager, entry);
                                Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                                JsonElement result = ToJsonElement(await tools["tablix_discover_table"](ParseArgs("{\"databaseId\":\"test_sqlite\",\"tableName\":\"users\"}")).ConfigureAwait(false));
                                Equal("users", result.GetProperty("Table").GetProperty("TableName").GetString(), "TableName mismatch.");
                                Equal(4, result.GetProperty("Table").GetProperty("Columns").GetArrayLength(), "Column count mismatch.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "ListRelationshipsReturnsForeignKeys", "MCP list relationships returns sample FKs", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            await WithTempSettingsManagerAsync(async manager =>
                            {
                                ConfigureOnlyDatabase(manager, entry);
                                Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                                JsonElement result = ToJsonElement(await tools["tablix_list_relationships"](ParseArgs("{\"databaseId\":\"test_sqlite\",\"maxResults\":10}")).ConfigureAwait(false));
                                True(result.GetProperty("Objects").EnumerateArray().Any(r => r.GetProperty("ToTable").GetString() == "users"), "Expected relationship to users.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "UpdateContextReplaceAndAppend", "MCP update context supports replace and append", async ct =>
                    {
                        await WithTempSettingsManagerAsync(async manager =>
                        {
                            Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                            JsonElement replace = ToJsonElement(await tools["tablix_update_context"](ParseArgs("{\"databaseId\":\"db_sample_sqlite\",\"context\":\"First\",\"mode\":\"replace\"}")).ConfigureAwait(false));
                            True(replace.GetProperty("Success").GetBoolean(), "Replace should succeed.");

                            JsonElement append = ToJsonElement(await tools["tablix_update_context"](ParseArgs("{\"databaseId\":\"db_sample_sqlite\",\"context\":\"Second\",\"mode\":\"append\"}")).ConfigureAwait(false));
                            True(append.GetProperty("Success").GetBoolean(), "Append should succeed.");
                            string context = manager.GetDatabase("db_sample_sqlite").Context;
                            Contains(context, "First", "Context should contain original text.");
                            Contains(context, "Second", "Context should contain appended text.");
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "ExecuteQueryRespectsAllowedQueries", "MCP execute query rejects disallowed statement type", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            entry.AllowedQueries = new List<string> { "SELECT" };
                            await WithTempSettingsManagerAsync(async manager =>
                            {
                                ConfigureOnlyDatabase(manager, entry);
                                Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                                JsonElement result = ToJsonElement(await tools["tablix_execute_query"](ParseArgs("{\"databaseId\":\"test_sqlite\",\"query\":\"DELETE FROM users\"}")).ConfigureAwait(false));
                                False(result.GetProperty("Success").GetBoolean(), "DELETE should be rejected.");
                                Contains(result.GetProperty("Error").GetString(), "DELETE", "Error should mention DELETE.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "ExecuteQueryReturnsRows", "MCP execute query returns actual result rows", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            entry.AllowedQueries = new List<string> { "SELECT" };
                            await WithTempSettingsManagerAsync(async manager =>
                            {
                                ConfigureOnlyDatabase(manager, entry);
                                Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                                JsonElement result = ToJsonElement(await tools["tablix_execute_query"](ParseArgs("{\"databaseId\":\"test_sqlite\",\"query\":\"SELECT COUNT(*) AS total_users FROM users\"}")).ConfigureAwait(false));
                                True(result.GetProperty("Success").GetBoolean(), "SELECT count should succeed.");
                                Equal(1, result.GetProperty("RowsReturned").GetInt32(), "Count query should return one row.");
                                JsonElement rows = result.GetProperty("Data").GetProperty("Rows");
                                Equal(1, rows.GetArrayLength(), "Data should contain one row.");
                                Equal(5, rows[0].GetProperty("total_users").GetInt32(), "Sample users count mismatch.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "MissingRequiredArgumentsReturnErrors", "MCP tools return useful errors for missing required arguments", async ct =>
                    {
                        await WithTempSettingsManagerAsync(async manager =>
                        {
                            Dictionary<string, Func<JsonElement?, Task<object>>> tools = RegisteredTools(manager, new CrawlCache());
                            JsonElement tableResult = ToJsonElement(await tools["tablix_discover_table"](ParseArgs("{\"databaseId\":\"db_sample_sqlite\"}")).ConfigureAwait(false));
                            Contains(tableResult.GetProperty("Error").GetString(), "tableName", "Missing tableName should be reported.");

                            JsonElement queryResult = ToJsonElement(await tools["tablix_execute_query"](ParseArgs("{\"databaseId\":\"db_sample_sqlite\"}")).ConfigureAwait(false));
                            False(queryResult.GetProperty("Success").GetBoolean(), "Missing query should fail.");
                            Contains(queryResult.GetProperty("Error").GetString(), "query", "Missing query should be reported.");
                        }).ConfigureAwait(false);
                    })
                });
        }

        /// <summary>
        /// MCP model-facing guidance tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor McpGuidanceSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "McpGuidance",
                displayName: "MCP Guidance",
                cases: new List<TestCaseDescriptor>
                {
                    Case("McpGuidance", "DiscoveryGuidance", "Database discovery tells models to start there and page", ct =>
                    {
                        Dictionary<string, string> descriptions = RegisteredToolDescriptions();
                        Contains(descriptions["tablix_discover_databases"], "Use first in every Tablix workflow", "Discovery tool should be the starting point.");
                        Contains(descriptions["tablix_discover_databases"], "NextSkip", "Discovery tool should explain pagination continuation.");
                        Contains(descriptions["tablix_discover_databases"], "AllowedQueries", "Discovery tool should surface query permission guidance.");
                        Contains(descriptions["tablix_discover_databases"], "Credentials are never returned", "Discovery tool should explicitly protect credentials.");
                        return Task.CompletedTask;
                    }),
                    Case("McpGuidance", "LargeSchemaGuidance", "Large schema guidance prefers paginated tools", ct =>
                    {
                        Dictionary<string, string> descriptions = RegisteredToolDescriptions();
                        Contains(descriptions["tablix_discover_database"], "can be very large", "Full database discovery should warn about size.");
                        Contains(descriptions["tablix_discover_database"], "tablix_list_tables", "Full database discovery should point to paginated table listing.");
                        Contains(descriptions["tablix_list_tables"], "Do not write SQL from this summary alone", "Table listing should prevent low-fidelity SQL generation.");
                        return Task.CompletedTask;
                    }),
                    Case("McpGuidance", "RelationshipGuidance", "Relationship guidance distinguishes declared and inferred relationships", ct =>
                    {
                        Dictionary<string, string> descriptions = RegisteredToolDescriptions();
                        Contains(descriptions["tablix_list_relationships"], "declared foreign keys only", "Relationship tool should define relationship source.");
                        Contains(descriptions["tablix_list_relationships"], "not proof that tables are unrelated", "Relationship tool should warn about absent FKs.");
                        Contains(descriptions["tablix_list_relationships"], "clearly label any inference", "Relationship tool should guide inferred relationship handling.");
                        return Task.CompletedTask;
                    }),
                    Case("McpGuidance", "QueryGuidance", "Query guidance requires schema and permission checks", ct =>
                    {
                        Dictionary<string, string> descriptions = RegisteredToolDescriptions();
                        Contains(descriptions["tablix_execute_query"], "AllowedQueries", "Query tool should mention AllowedQueries.");
                        Contains(descriptions["tablix_execute_query"], "Prefer SELECT for exploration", "Query tool should prefer read-only exploration.");
                        Contains(descriptions["tablix_execute_query"], "Validate table and column names", "Query tool should require schema validation.");
                        Contains(descriptions["tablix_execute_query"], "Do not merely provide SQL", "Query tool should tell models to run queries for answer requests.");
                        Contains(descriptions["tablix_execute_query"], "how many", "Query tool should identify count-style requests as executable.");
                        Contains(descriptions["tablix_execute_query"], "permitted query", "Query tool should generalize beyond SELECT.");
                        Contains(descriptions["tablix_execute_query"], "add, update, or delete", "Query tool should mention permitted write requests.");
                        Contains(descriptions["tablix_execute_query"], "bad or unknown column", "Query tool should explain schema refresh after unknown column errors.");
                        Contains(descriptions["tablix_execute_query"], "column type mismatch", "Query tool should explain schema refresh after type errors.");
                        Contains(descriptions["tablix_execute_query"], "update Context", "Query tool should direct agents to correct stale context.");
                        return Task.CompletedTask;
                    }),
                    Case("McpGuidance", "ContextGuidance", "Context update guidance protects persisted context quality", ct =>
                    {
                        Dictionary<string, string> descriptions = RegisteredToolDescriptions();
                        Contains(descriptions["tablix_update_context"], "explicit user instruction", "Context update should not be casual.");
                        Contains(descriptions["tablix_update_context"], "Do not store secrets", "Context update should protect sensitive data.");
                        Contains(descriptions["tablix_update_context"], "separate declared relationships from inferred ones", "Context update should preserve relationship fidelity.");
                        Contains(descriptions["tablix_update_context"], "wrong column names", "Context update should correct stale column names.");
                        Contains(descriptions["tablix_update_context"], "wrong column types", "Context update should correct stale column types.");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Docker packaging tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor DockerPackagingSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DockerPackaging",
                displayName: "Docker Packaging",
                cases: new List<TestCaseDescriptor>
                {
                    Case("DockerPackaging", "DashboardProxyPreservesApiRequestUri", "Dashboard proxy preserves API path and query string", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string entrypointFilename = Path.Combine(repositoryRoot, "dashboard", "entrypoint.sh");
                        string nginxFilename = Path.Combine(repositoryRoot, "dashboard", "nginx.conf");

                        string entrypoint = File.ReadAllText(entrypointFilename);
                        string nginx = File.ReadAllText(nginxFilename);

                        Contains(entrypoint, "proxy_pass \\$backend\\$request_uri;", "Runtime nginx config must preserve the full API request URI.");
                        DoesNotContain(entrypoint, "proxy_pass \\$backend/v1/;", "Runtime nginx config must not rewrite API requests to /v1/.");
                        Contains(nginx, "proxy_pass http://localhost:9100$request_uri;", "Fallback nginx config must preserve the full API request URI.");
                        return Task.CompletedTask;
                    })
                });
        }

        /// <summary>
        /// Dashboard-to-REST API contract tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor DashboardApiContractSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "DashboardApiContract",
                displayName: "Dashboard API Contract",
                cases: new List<TestCaseDescriptor>
                {
                    Case("DashboardApiContract", "DashboardCallsRegisteredServerRoutes", "Dashboard API calls match registered server routes", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string serverRoutes = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "TablixServer.cs"));
                        string dashboardSource = ReadAllDashboardSource(repositoryRoot);

                        List<ApiRouteContract> contracts = DashboardApiContracts();
                        foreach (ApiRouteContract contract in contracts)
                        {
                            Contains(serverRoutes, contract.ServerRegistrationFragment, "Server route is missing: " + contract.Method + " " + contract.RouteTemplate);
                            if (contract.RequiredDashboardFragment != null)
                                Contains(dashboardSource, contract.RequiredDashboardFragment, "Dashboard call is missing: " + contract.Method + " " + contract.RouteTemplate);
                        }

                        foreach (Match match in Regex.Matches(dashboardSource, "/v1/[A-Za-z0-9_${}./?=&-]+"))
                        {
                            string route = NormalizeDashboardRoute(match.Value);
                            bool known = contracts.Any(contract => route.StartsWith(contract.DashboardRoutePrefix, StringComparison.Ordinal));
                            True(known, "Dashboard calls unknown API route fragment: " + match.Value);
                        }

                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ApiFetchDoesNotForceJsonOnBodylessRequests", "Dashboard API helper only sends JSON content type with request bodies", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string client = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "api", "client.ts"));

                        Contains(client, "if (options.body && !headers.has('Content-Type'))", "apiFetch should only set JSON content type when a request body is present.");
                        DoesNotContain(client, "'Content-Type': 'application/json'", "apiFetch should not force JSON content type on every request.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ChatTranscriptAdaptsToToolCallHeightChanges", "Chat transcript adapts when tool calls expand and collapse", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ChatPage.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(chatPage, "ResizeObserver", "Chat transcript should observe rendered content height changes.");
                        Contains(chatPage, "onToggle={onToolCallToggled}", "Tool call details should notify the transcript after expand or collapse.");
                        Contains(chatPage, "onToolCallToggleStart={captureTranscriptStickiness}", "Tool call toggles should capture whether the user was already at the bottom.");
                        Contains(stylesheet, ".chat-transcript-content", "Chat transcript should have an observed content wrapper.");
                        Contains(stylesheet, "flex: 0 0 auto;", "Chat messages should not shrink and clip inside the transcript.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ChatSelectorsResetConversation", "Changing chat database or provider clears the conversation", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ChatPage.tsx"));

                        Contains(chatPage, "function resetConversation()", "Chat page should centralize conversation reset behavior.");
                        Contains(chatPage, "setMessages([]);", "Conversation reset should clear chat messages.");
                        Contains(chatPage, "setInput('');", "Conversation reset should clear pending input.");
                        Contains(chatPage, "function handleDatabaseChanged", "Database selector should use a reset-aware change handler.");
                        Contains(chatPage, "function handleProviderChanged", "Provider selector should use a reset-aware change handler.");
                        Contains(chatPage, "onChange={event => handleDatabaseChanged(event.target.value)}", "Database selector should reset the conversation when changed.");
                        Contains(chatPage, "onChange={event => handleProviderChanged(event.target.value)}", "Provider selector should reset the conversation when changed.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "TopbarHeightIsFixed", "Dashboard topbar keeps a fixed vertical size", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string navbar = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "Navbar.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(navbar, "className=\"topbar\"", "Navbar should use the fixed topbar class.");
                        Contains(navbar, "position: 'fixed'", "Navbar should be fixed to the viewport instead of participating in page scroll.");
                        Contains(navbar, "left: 0", "Navbar should be pinned to the left edge.");
                        Contains(navbar, "right: 0", "Navbar should be pinned to the right edge.");
                        Contains(navbar, "flex: '0 0 52px'", "Navbar should not shrink as an app-shell flex child.");
                        Contains(navbar, "minHeight: '52px'", "Navbar should keep a fixed minimum height.");
                        Contains(navbar, "maxHeight: '52px'", "Navbar should keep a fixed maximum height.");
                        Contains(stylesheet, "padding-top: 52px;", "App shell should reserve fixed topbar space.");
                        Contains(stylesheet, ".topbar", "Stylesheet should define the fixed topbar contract.");
                        Contains(stylesheet, "position: fixed;", "Topbar stylesheet should keep the topbar independent from scroll containers.");
                        Contains(stylesheet, "inset: 0 0 auto 0;", "Topbar stylesheet should pin the bar to the viewport top.");
                        Contains(stylesheet, "flex: 0 0 52px;", "Topbar stylesheet should prevent flex shrink.");
                        Contains(stylesheet, "min-height: 52px;", "Topbar stylesheet should pin minimum height.");
                        Contains(stylesheet, "max-height: 52px;", "Topbar stylesheet should pin maximum height.");
                        return Task.CompletedTask;
                    })
                });
        }

        private static TestCaseDescriptor Case(string suiteId, string caseId, string displayName, Func<CancellationToken, Task> executeAsync)
        {
            return new TestCaseDescriptor(suiteId, caseId, displayName, executeAsync);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string entrypointFilename = Path.Combine(directory.FullName, "dashboard", "entrypoint.sh");
                string solutionFilename = Path.Combine(directory.FullName, "src", "Tablix.slnx");

                if (File.Exists(entrypointFilename) && File.Exists(solutionFilename))
                    return directory.FullName;

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Unable to find Tablix repository root from " + AppContext.BaseDirectory + ".");
        }

        private static string ReadAllDashboardSource(string repositoryRoot)
        {
            string dashboardSourceDirectory = Path.Combine(repositoryRoot, "dashboard", "src");
            List<string> sourceFiles = Directory
                .GetFiles(dashboardSourceDirectory, "*.*", SearchOption.AllDirectories)
                .Where(filename =>
                    filename.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                    filename.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
                .OrderBy(filename => filename)
                .ToList();

            return String.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));
        }

        private static string NormalizeDashboardRoute(string route)
        {
            string normalized = route;
            int queryIndex = normalized.IndexOf('?');
            if (queryIndex >= 0)
                normalized = normalized.Substring(0, queryIndex);

            normalized = normalized.Replace("${id}", "{id}", StringComparison.Ordinal);
            normalized = normalized.Replace("${selectedDb}", "{id}", StringComparison.Ordinal);
            normalized = normalized.Replace("${db.Id}", "{id}", StringComparison.Ordinal);
            normalized = normalized.Replace("${contextTarget.Id}", "{id}", StringComparison.Ordinal);
            normalized = normalized.Replace("${detail.DatabaseId}", "{id}", StringComparison.Ordinal);
            return normalized;
        }

        private static List<ApiRouteContract> DashboardApiContracts()
        {
            return new List<ApiRouteContract>
            {
                new ApiRouteContract("GET", "/v1/database", "rest.Get(\"/v1/database\"", "/v1/database?", "/v1/database"),
                new ApiRouteContract("GET", "/v1/database/{id}", "rest.Get(\"/v1/database/{id}\"", "/v1/database/${id}", "/v1/database/{id}"),
                new ApiRouteContract("POST", "/v1/database", "rest.Post<DatabaseEntry>(\"/v1/database\"", "apiFetch('/v1/database', { method: 'POST'", "/v1/database"),
                new ApiRouteContract("PUT", "/v1/database/{id}", "rest.Put<DatabaseEntry>(\"/v1/database/{id}\"", "method: 'PUT'", "/v1/database/{id}"),
                new ApiRouteContract("DELETE", "/v1/database/{id}", "rest.Delete(\"/v1/database/{id}\"", "method: 'DELETE'", "/v1/database/{id}"),
                new ApiRouteContract("POST", "/v1/database/{id}/context", "rest.Post<ContextUpdateRequest>(\"/v1/database/{id}/context\"", "/context`", "/v1/database/{id}/context"),
                new ApiRouteContract("POST", "/v1/database/{id}/context/build", "rest.Post<BuildContextRequest>(\"/v1/database/{id}/context/build\"", "/context/build", "/v1/database/{id}/context/build"),
                new ApiRouteContract("POST", "/v1/database/{id}/crawl/stream", "rest.Post(\"/v1/database/{id}/crawl/stream\"", "/crawl/stream", "/v1/database/{id}/crawl/stream"),
                new ApiRouteContract("POST", "/v1/database/{id}/query", "rest.Post<QueryRequest>(\"/v1/database/{id}/query\"", "/query`", "/v1/database/{id}/query"),
                new ApiRouteContract("GET", "/v1/chat/options", "rest.Get(\"/v1/chat/options\"", "/v1/chat/options", "/v1/chat/options"),
                new ApiRouteContract("POST", "/v1/chat", "rest.Post<ChatRequest>(\"/v1/chat\"", "apiFetch('/v1/chat',", "/v1/chat"),
                new ApiRouteContract("POST", "/v1/chat/stream", "rest.Post<ChatRequest>(\"/v1/chat/stream\"", "/v1/chat/stream", "/v1/chat/stream"),
                new ApiRouteContract("GET", "/v1/settings", "rest.Get(\"/v1/settings\"", "apiFetch('/v1/settings')", "/v1/settings"),
                new ApiRouteContract("PUT", "/v1/settings", "rest.Put<SettingsUpdateRequest>(\"/v1/settings\"", "method: 'PUT'", "/v1/settings")
            };
        }

        private class ApiRouteContract
        {
            public string Method { get; }
            public string RouteTemplate { get; }
            public string ServerRegistrationFragment { get; }
            public string RequiredDashboardFragment { get; }
            public string DashboardRoutePrefix { get; }

            public ApiRouteContract(
                string method,
                string routeTemplate,
                string serverRegistrationFragment,
                string requiredDashboardFragment,
                string dashboardRoutePrefix)
            {
                Method = method;
                RouteTemplate = routeTemplate;
                ServerRegistrationFragment = serverRegistrationFragment;
                RequiredDashboardFragment = requiredDashboardFragment;
                DashboardRoutePrefix = dashboardRoutePrefix;
            }
        }

        private static DatabaseDetail SampleDetail()
        {
            return new DatabaseDetail
            {
                DatabaseId = "sample",
                Context = "Sample database",
                IsCrawled = true,
                Tables = new List<TableDetail>
                {
                    new TableDetail
                    {
                        SchemaName = "main",
                        TableName = "line_items",
                        Columns = new List<ColumnDetail>
                        {
                            new ColumnDetail { ColumnName = "Id" },
                            new ColumnDetail { ColumnName = "OrderId" }
                        },
                        ForeignKeys = new List<ForeignKeyDetail>
                        {
                            new ForeignKeyDetail { ConstraintName = "fk_line_items_orders", ColumnName = "OrderId", ReferencedTable = "orders", ReferencedColumn = "Id" }
                        }
                    },
                    new TableDetail
                    {
                        SchemaName = "main",
                        TableName = "orders",
                        Columns = new List<ColumnDetail>
                        {
                            new ColumnDetail { ColumnName = "Id" },
                            new ColumnDetail { ColumnName = "UserId" }
                        },
                        ForeignKeys = new List<ForeignKeyDetail>
                        {
                            new ForeignKeyDetail { ConstraintName = "fk_orders_users", ColumnName = "UserId", ReferencedTable = "users", ReferencedColumn = "Id" }
                        }
                    },
                    new TableDetail
                    {
                        SchemaName = "main",
                        TableName = "users",
                        Columns = new List<ColumnDetail>
                        {
                            new ColumnDetail { ColumnName = "Id" }
                        }
                    }
                }
            };
        }

        private static Dictionary<string, string> RegisteredToolDescriptions()
        {
            Dictionary<string, string> descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            WithTempSettingsFile(filename =>
            {
                SettingsManager settingsManager = new SettingsManager(filename);
                CrawlCache crawlCache = new CrawlCache();

                McpToolRegistrar.RegisterAll(
                    (name, description, inputSchema, handler) =>
                    {
                        descriptions[name] = description;
                    },
                    settingsManager,
                    crawlCache);
            });

            return descriptions;
        }

        private static Dictionary<string, Func<JsonElement?, Task<object>>> RegisteredTools(
            SettingsManager settingsManager,
            CrawlCache crawlCache)
        {
            Dictionary<string, Func<JsonElement?, Task<object>>> tools = new Dictionary<string, Func<JsonElement?, Task<object>>>(StringComparer.OrdinalIgnoreCase);

            McpToolRegistrar.RegisterAll(
                (name, description, inputSchema, handler) =>
                {
                    tools[name] = handler;
                },
                settingsManager,
                crawlCache);

            return tools;
        }

        private static JsonElement? ParseArgs(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                return document.RootElement.Clone();
            }
        }

        private static JsonElement ToJsonElement(object obj)
        {
            string json = Serializer.SerializeJson(obj);
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                return document.RootElement.Clone();
            }
        }

        private static void ConfigureOnlyDatabase(SettingsManager manager, DatabaseEntry entry)
        {
            List<string> ids = manager.Settings.Databases.Select(db => db.Id).ToList();
            foreach (string id in ids)
            {
                manager.DeleteDatabase(id);
            }

            manager.AddDatabase(entry);
        }

        private static async Task WithTempSettingsManagerAsync(Func<SettingsManager, Task> action)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "tablix_settings_" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                SettingsManager manager = new SettingsManager(tempFile);
                await action(manager).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(tempFile);
            }
        }

        private static async Task WithCustomSqliteDatabaseAsync(string setupSql, Func<DatabaseEntry, Task> action)
        {
            string tempDbPath = Path.Combine(Path.GetTempPath(), "tablix_custom_" + Guid.NewGuid().ToString("N") + ".db");

            try
            {
                using (SqliteConnection connection = new SqliteConnection("Data Source=" + tempDbPath))
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                    using (SqliteCommand command = new SqliteCommand(setupSql, connection))
                    {
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }

                DatabaseEntry entry = new DatabaseEntry
                {
                    Id = "custom_sqlite",
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = tempDbPath,
                    Schema = "main",
                    DatabaseName = "custom"
                };

                await action(entry).ConfigureAwait(false);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                TryDelete(tempDbPath);
                TryDelete(tempDbPath + "-wal");
                TryDelete(tempDbPath + "-shm");
            }
        }

        private static async Task WithTempDatabaseAsync(Func<DatabaseEntry, Task> action)
        {
            string sourceDb = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "docker",
                "database.db"));

            if (!File.Exists(sourceDb))
            {
                sourceDb = Path.GetFullPath(Path.Combine(
                    AppContext.BaseDirectory,
                    "..",
                    "..",
                    "..",
                    "..",
                    "..",
                    "docker",
                    "database.db"));
            }

            string tempDbPath = Path.Combine(Path.GetTempPath(), "tablix_test_" + Guid.NewGuid().ToString("N") + ".db");
            File.Copy(sourceDb, tempDbPath, true);

            try
            {
                DatabaseEntry entry = new DatabaseEntry
                {
                    Id = "test_sqlite",
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = tempDbPath
                };

                await action(entry).ConfigureAwait(false);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                TryDelete(tempDbPath);
                TryDelete(tempDbPath + "-wal");
                TryDelete(tempDbPath + "-shm");
            }
        }

        private static void WithTempSettingsFile(Action<string> action)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "tablix_settings_" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                action(tempFile);
            }
            finally
            {
                TryDelete(tempFile);
            }
        }

        private static void TryDelete(string filename)
        {
            try
            {
                if (File.Exists(filename))
                    File.Delete(filename);
            }
            catch (IOException)
            {
            }
        }

        private static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(message + " Expected '" + expected + "' but got '" + actual + "'.");
        }

        private static void True(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void False(bool condition, string message)
        {
            if (condition)
                throw new InvalidOperationException(message);
        }

        private static void Null(object value, string message)
        {
            if (value != null)
                throw new InvalidOperationException(message);
        }

        private static void NotNull(object value, string message)
        {
            if (value == null)
                throw new InvalidOperationException(message);
        }

        private static void Contains(string value, string expectedSubstring, string message)
        {
            if (value == null || !value.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(message);
        }

        private static void DoesNotContain(string value, string unexpectedSubstring, string message)
        {
            if (value != null && value.Contains(unexpectedSubstring, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(message);
        }

        private static void Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("Expected exception of type " + typeof(TException).Name + ".");
        }

        private static void ThrowsAny(Action action)
        {
            try
            {
                action();
            }
            catch (Exception)
            {
                return;
            }

            throw new InvalidOperationException("Expected an exception.");
        }

        private static async Task ThrowsAsync<TException>(Func<Task> action) where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException)
            {
                return;
            }

            throw new InvalidOperationException("Expected exception of type " + typeof(TException).Name + ".");
        }

        private static async Task ThrowsAnyAsync(Func<Task> action)
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (Exception)
            {
                return;
            }

            throw new InvalidOperationException("Expected an exception.");
        }
    }
}
