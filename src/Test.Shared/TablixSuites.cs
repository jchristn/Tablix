namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Tablix.Core.DatabaseDrivers;
    using Tablix.Core.Enums;
    using Tablix.Core.Helpers;
    using Tablix.Core.Models;
    using Tablix.Core.Persistence;
    using Tablix.Core.Persistence.Sqlite;
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
                    Case("SettingsClamping", "PersistenceNullIgnored", "TablixSettings Persistence null assignment is ignored", ct =>
                    {
                        TablixSettings settings = new TablixSettings();
                        settings.Persistence = null;
                        NotNull(settings.Persistence, "Persistence should not be null.");
                        Equal(TablixPersistenceDatabaseTypeEnum.Sqlite, settings.Persistence.Type, "Persistence type mismatch.");
                        Equal("tablix.db", settings.Persistence.Filename, "Persistence filename mismatch.");
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
                    }),
                    Case("SettingsClamping", "ProviderMaxConcurrentRequestsClamps", "Model provider MaxConcurrentRequests clamps to range", ct =>
                    {
                        ModelProviderSettings provider = new ModelProviderSettings();
                        provider.MaxConcurrentRequests = 0;
                        Equal(1, provider.MaxConcurrentRequests, "MaxConcurrentRequests low clamp mismatch.");
                        provider.MaxConcurrentRequests = 32;
                        Equal(16, provider.MaxConcurrentRequests, "MaxConcurrentRequests high clamp mismatch.");
                        provider.MaxConcurrentRequests = 4;
                        Equal(4, provider.MaxConcurrentRequests, "MaxConcurrentRequests valid value mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsClamping", "DefaultProvidersUseNativeToolsWhenSupported", "Default providers enable native tool use when native tools are supported", ct =>
                    {
                        List<ModelProviderSettings> providers = new List<ModelProviderSettings>
                        {
                            DefaultDataFactory.CreateOllamaProvider(),
                            DefaultDataFactory.CreateOpenAiProvider(),
                            DefaultDataFactory.CreateOpenAiCompatibleProvider(),
                            DefaultDataFactory.CreateGeminiProvider()
                        };

                        foreach (ModelProviderSettings provider in providers)
                        {
                            if (provider.SupportsNativeToolCalls)
                                True(provider.UseNativeToolCalls, provider.Id + " should use native tools when native tools are supported.");
                            True(String.IsNullOrWhiteSpace(provider.ToolCapabilityNote), provider.Id + " should not seed a user-facing tool capability note.");
                        }

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
                        Equal(original.Persistence.Type, restored.Persistence.Type, "Persistence type mismatch.");
                        Equal(original.Persistence.Filename, restored.Persistence.Filename, "Persistence filename mismatch.");
                        Equal(original.ApiKeys.Count, restored.ApiKeys.Count, "API key count mismatch.");
                        NotNull(restored.Chat, "Chat settings should not be null.");
                        Equal(original.Chat.Enabled, restored.Chat.Enabled, "Chat enabled mismatch.");
                        Equal(original.Chat.DefaultProviderId, restored.Chat.DefaultProviderId, "Default provider mismatch.");
                        Equal(original.Chat.DefaultStreaming, restored.Chat.DefaultStreaming, "Default streaming mismatch.");
                        Equal(original.Chat.MaxContextTables, restored.Chat.MaxContextTables, "Max context tables mismatch.");
                        Equal(original.Chat.Tools.MaxToolIterations, restored.Chat.Tools.MaxToolIterations, "Max tool iterations mismatch.");
                        Equal(original.Chat.PromptProcessing.PreferNativeToolCalls, restored.Chat.PromptProcessing.PreferNativeToolCalls, "Prefer native tools mismatch.");
                        Equal(original.Chat.PromptProcessing.FallbackWhenNativeToolNotCalled, restored.Chat.PromptProcessing.FallbackWhenNativeToolNotCalled, "Fallback setting mismatch.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "PascalCaseProperties", "Serialization uses PascalCase property names", ct =>
                    {
                        string json = Serializer.SerializeJson(new TablixSettings());
                        Contains(json, "Rest", "Expected Rest property.");
                        Contains(json, "Logging", "Expected Logging property.");
                        Contains(json, "Persistence", "Expected Persistence property.");
                        Contains(json, "Chat", "Expected Chat property.");
                        DoesNotContain(json, "Providers", "Providers should not be serialized in tablix.json.");
                        DoesNotContain(json, "Databases", "Databases should not be serialized in tablix.json.");
                        Contains(json, "ApiKeys", "Expected ApiKeys property.");
                        return Task.CompletedTask;
                    }),
                    Case("SettingsSerialization", "DefaultChatPromptProcessing", "Default settings include chat prompt processing", ct =>
                    {
                        TablixSettings settings = new TablixSettings();

                        NotNull(settings.Chat, "Chat settings should not be null.");
                        Contains(settings.Chat.SystemPrompt, "Restrict your conversation to only the selected database", "Default chat prompt should restrict scope.");
                        Contains(settings.Chat.SystemPrompt, "execute the query", "Default chat prompt should tell models to execute answerable data queries.");
                        Contains(settings.Chat.SystemPrompt, "instead of only describing SQL", "Default chat prompt should discourage SQL-only answers when a tool can execute.");
                        Contains(settings.Chat.SystemPrompt, "Never fabricate table contents", "Default chat prompt should prohibit fabricated database facts.");
                        Contains(settings.Chat.SystemPrompt, "Return SQL text only when", "Default chat prompt should limit SQL-only answers to explicit requests or unavailable execution.");
                        Contains(settings.Chat.SystemPrompt, "one permitted SQL statement", "Default chat prompt should give concise query tool usage guidance.");
                        Contains(settings.Chat.SystemPrompt, "bad or unknown column", "Default chat prompt should handle unknown column failures.");
                        Contains(settings.Chat.SystemPrompt, "column type mismatch", "Default chat prompt should handle column type failures.");
                        Contains(settings.Chat.SystemPrompt, "update database context", "Default chat prompt should correct stale database context.");
                        Contains(settings.Chat.SystemPrompt, "update table context", "Default chat prompt should correct stale table context.");
                        True(settings.Chat.PromptProcessing.PreferNativeToolCalls, "Prompt processing should prefer native tools by default.");
                        True(settings.Chat.PromptProcessing.RequireExecutionForDataRequests, "Prompt processing should require execution for data requests by default.");
                        True(settings.Chat.PromptProcessing.FallbackWhenNativeToolNotCalled, "Prompt processing should enable server fallback by default.");
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
        /// Settings manager and persistence integration tests.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SettingsManagerSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Persistence",
                displayName: "Persistence",
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
                    Case("Persistence", "DatabaseCreateRead", "Persistence creates and reads database entries", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            DatabaseEntry created = await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "test_db_1" }, ct).ConfigureAwait(false);
                            DatabaseEntry retrieved = await driver.DatabaseConnections.ReadAsync(created.Id, ct).ConfigureAwait(false);
                            NotNull(retrieved, "Database should be found.");
                            Equal("test_db_1", retrieved.Id, "Database ID mismatch.");
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "DatabaseUpdate", "Persistence updates database entries", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            DatabaseEntry entry = new DatabaseEntry { Id = "update_db", DatabaseName = "OriginalName" };
                            await driver.DatabaseConnections.CreateAsync(entry, ct).ConfigureAwait(false);
                            DatabaseEntry updated = new DatabaseEntry { Id = "update_db", DatabaseName = "UpdatedName" };
                            await driver.DatabaseConnections.UpdateAsync(updated, true, ct).ConfigureAwait(false);
                            DatabaseEntry retrieved = await driver.DatabaseConnections.ReadAsync("update_db", ct).ConfigureAwait(false);
                            Equal("UpdatedName", retrieved.DatabaseName, "DatabaseName mismatch.");
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "DatabaseDelete", "Persistence deletes database entries", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "delete_db" }, ct).ConfigureAwait(false);
                            await driver.DatabaseConnections.DeleteAsync("delete_db", ct).ConfigureAwait(false);
                            Null(await driver.DatabaseConnections.ReadAsync("delete_db", ct).ConfigureAwait(false), "Database should be deleted.");
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "DuplicateDatabaseThrows", "Duplicate database create throws", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "dup_db" }, ct).ConfigureAwait(false);
                            await ThrowsAnyAsync(async () => await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "dup_db" }, ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "DeleteUnknownReturnsFalse", "Deleting unknown database returns false", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            False(await driver.DatabaseConnections.DeleteAsync("nonexistent_db", ct).ConfigureAwait(false), "Unknown delete should return false.");
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "DatabaseReadCaseInsensitive", "Database reads are case-insensitive", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "Case_Db" }, ct).ConfigureAwait(false);
                            NotNull(await driver.DatabaseConnections.ReadAsync("case_db", ct).ConfigureAwait(false), "Database should be found case-insensitively.");
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "PersistsAcrossDrivers", "Persisted database is visible to a new driver", async ct =>
                    {
                        string filename = GetTempDatabaseFilename();
                        try
                        {
                            SqliteDatabaseDriver first = new SqliteDatabaseDriver(filename);
                            await first.InitializeAsync(ct).ConfigureAwait(false);
                            await first.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "persisted_db", DatabaseName = "Persisted" }, ct).ConfigureAwait(false);

                            SqliteDatabaseDriver second = new SqliteDatabaseDriver(filename);
                            await second.InitializeAsync(ct).ConfigureAwait(false);
                            DatabaseEntry retrieved = await second.DatabaseConnections.ReadAsync("persisted_db", ct).ConfigureAwait(false);
                            NotNull(retrieved, "Persisted database should be found.");
                            Equal("Persisted", retrieved.DatabaseName, "Persisted database name mismatch.");
                        }
                        finally
                        {
                            TryDelete(filename);
                        }
                    }),
                    Case("Persistence", "MissingNestedSqliteFileIsCreated", "Missing nested SQLite persistence file is created and initialized", async ct =>
                    {
                        string rootDirectory = Path.Combine(Path.GetTempPath(), "tablix_persistence_" + Guid.NewGuid().ToString("N"));
                        string filename = Path.Combine(rootDirectory, "state", "tablix.db");

                        try
                        {
                            SqliteDatabaseDriver driver = new SqliteDatabaseDriver(filename);
                            await driver.InitializeAsync(ct).ConfigureAwait(false);

                            True(File.Exists(filename), "SQLite persistence file should be created when it is missing.");
                            long providerCount = await driver.ModelProviders.CountAsync(null, null, ct).ConfigureAwait(false);
                            True(providerCount > 0, "SQLite persistence file should be initialized with default providers.");
                        }
                        finally
                        {
                            TryDeleteDirectory(rootDirectory);
                        }
                    }),
                    Case("Persistence", "EmptyDirectoryAtSqliteFilenameIsReplaced", "Empty directory at SQLite persistence filename is replaced", async ct =>
                    {
                        string rootDirectory = Path.Combine(Path.GetTempPath(), "tablix_persistence_" + Guid.NewGuid().ToString("N"));
                        string filename = Path.Combine(rootDirectory, "tablix.db");

                        try
                        {
                            Directory.CreateDirectory(filename);
                            SqliteDatabaseDriver driver = new SqliteDatabaseDriver(filename);
                            await driver.InitializeAsync(ct).ConfigureAwait(false);
                            True(File.Exists(filename), "Empty directory at SQLite filename should be replaced by a database file.");
                        }
                        finally
                        {
                            TryDeleteDirectory(rootDirectory);
                        }
                    }),
                    Case("Persistence", "FailedCrawlWriteRollsBack", "Failed crawl metadata write rolls back partial rows", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "rollback_db" }, ct).ConfigureAwait(false);

                            DatabaseDetail detail = new DatabaseDetail
                            {
                                DatabaseId = "rollback_db",
                                IsCrawled = true,
                                CrawledUtc = DateTime.UtcNow,
                                Tables = new List<TableDetail>
                                {
                                    new TableDetail
                                    {
                                        SchemaName = "main",
                                        TableName = "broken",
                                        Columns = new List<ColumnDetail>
                                        {
                                            new ColumnDetail { ColumnName = "duplicate_id", DataType = "INTEGER" },
                                            new ColumnDetail { ColumnName = "duplicate_id", DataType = "INTEGER" }
                                        }
                                    }
                                }
                            };

                            await ThrowsAnyAsync(async () => await driver.DatabaseMetadata.SaveCrawlAsync(detail, ct).ConfigureAwait(false)).ConfigureAwait(false);

                            DatabaseDetail persisted = await driver.DatabaseMetadata.ReadDetailAsync("rollback_db", ct).ConfigureAwait(false);
                            Null(persisted, "Failed crawl metadata write should not leave a partial crawl or table rows.");
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "SetupDismissHidesWizardWithoutCompleting", "Setup dismissal hides wizard without marking setup complete", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            SetupStateRead dismissed = await driver.SetupState.DismissAsync(ct).ConfigureAwait(false);
                            Equal(SetupWizardStatusEnum.NotStarted, dismissed.Status, "Dismissal should not complete setup.");
                            NotNull(dismissed.DismissedUtc, "Dismissed timestamp should be stored.");
                            False(dismissed.ShouldShowWizard, "Dismissed setup should not show the wizard.");
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "ConcurrentOperationsPreserveSqliteIntegrity", "Concurrent persistence operations preserve SQLite file integrity", async ct =>
                    {
                        string filename = GetTempDatabaseFilename();
                        try
                        {
                            SqliteDatabaseDriver driver = new SqliteDatabaseDriver(filename);
                            await driver.InitializeAsync(ct).ConfigureAwait(false);
                            await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "concurrent_db" }, ct).ConfigureAwait(false);

                            List<Task> tasks = new List<Task>();
                            for (int index = 0; index < 40; index++)
                            {
                                int operationIndex = index;
                                tasks.Add(Task.Run(async () =>
                                {
                                    string context = "context " + operationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    await driver.DatabaseContexts.UpsertAsync("concurrent_db", context, "replace", "test", ct).ConfigureAwait(false);
                                    DatabaseEntry database = await driver.DatabaseConnections.ReadAsync("concurrent_db", ct).ConfigureAwait(false);
                                    NotNull(database, "Concurrent read should find the database.");
                                    List<DatabaseEntry> databases = await driver.DatabaseConnections.EnumerateAsync(100, 0, null, ct).ConfigureAwait(false);
                                    True(databases.Count > 0, "Concurrent enumeration should return databases.");
                                }, ct));
                            }

                            await Task.WhenAll(tasks).ConfigureAwait(false);
                            driver.Dispose();

                            using SqliteConnection connection = new SqliteConnection("Data Source=" + filename + ";Pooling=False");
                            await connection.OpenAsync(ct).ConfigureAwait(false);
                            using SqliteCommand command = connection.CreateCommand();
                            command.CommandText = "PRAGMA integrity_check";
                            object result = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
                            Equal("ok", Convert.ToString(result), "SQLite integrity_check should pass after concurrent persistence operations.");
                        }
                        finally
                        {
                            TryDelete(filename);
                            TryDelete(filename + "-wal");
                            TryDelete(filename + "-shm");
                        }
                    }),
                    Case("Persistence", "ConcurrentContextAppendsAreNotLost", "Concurrent context appends are serialized atomically", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "append_db" }, ct).ConfigureAwait(false);

                            List<Task> tasks = new List<Task>();
                            for (int index = 0; index < 20; index++)
                            {
                                int operationIndex = index;
                                tasks.Add(Task.Run(async () =>
                                {
                                    string context = "append-marker-" + operationIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                    await driver.DatabaseContexts.UpsertAsync("append_db", context, "append", "test", ct).ConfigureAwait(false);
                                }, ct));
                            }

                            await Task.WhenAll(tasks).ConfigureAwait(false);
                            string persisted = await driver.DatabaseContexts.ReadAsync("append_db", ct).ConfigureAwait(false);
                            for (int index = 0; index < 20; index++)
                            {
                                string expected = "append-marker-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                Contains(persisted, expected, "Concurrent append should preserve '" + expected + "'.");
                            }
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "TableContextMissingTableThrowsKeyNotFound", "Table context write validates table metadata before insert", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await driver.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "missing_table_db" }, ct).ConfigureAwait(false);
                            await ThrowsAsync<KeyNotFoundException>(async () =>
                                await driver.TableContexts.UpsertAsync("missing_table_db", "tbl_missing", "context", "replace", "test", ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("SettingsManager", "ReloadReadsDisk", "Reload reads updated settings from disk", ct =>
                    {
                        WithTempSettingsFile(filename =>
                        {
                            SettingsManager manager = new SettingsManager(filename);
                            TablixSettings settings = manager.Settings;
                            settings.Persistence.Filename = "custom.db";
                            File.WriteAllText(filename, Serializer.SerializeJson(settings), System.Text.Encoding.UTF8);

                            manager.Reload();
                            Equal("custom.db", manager.Settings.Persistence.Filename, "Reloaded persistence filename mismatch.");
                        });
                        return Task.CompletedTask;
                    }),
                    Case("Persistence", "CreateNullThrows", "Create database null throws ArgumentNullException", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await ThrowsAsync<ArgumentNullException>(async () => await driver.DatabaseConnections.CreateAsync(null, ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "DeleteNullThrows", "Delete database null throws ArgumentNullException", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await ThrowsAsync<ArgumentNullException>(async () => await driver.DatabaseConnections.DeleteAsync(null, ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("Persistence", "UpdateUnknownThrows", "Update database unknown ID throws KeyNotFoundException", async ct =>
                    {
                        await WithTempPersistenceAsync(async driver =>
                        {
                            await ThrowsAsync<KeyNotFoundException>(async () => await driver.DatabaseConnections.UpdateAsync(new DatabaseEntry { Id = "missing_db" }, true, ct).ConfigureAwait(false)).ConfigureAwait(false);
                        }).ConfigureAwait(false);
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
                            Enabled = true,
                            SupportsNativeToolCalls = true,
                            UseNativeToolCalls = true,
                            SupportsStrictJson = true,
                            ToolCapabilityNote = "Native tools supported.",
                            MaxConcurrentRequests = 4
                        };

                        ModelProviderSummary summary = ModelProviderSummary.From(provider);
                        string json = Serializer.SerializeJson(summary, false);
                        True(summary.HasApiKey, "HasApiKey should indicate configured key.");
                        True(summary.SupportsNativeToolCalls, "Tool support should be exposed.");
                        True(summary.UseNativeToolCalls, "Native tool usage should be exposed.");
                        True(summary.SupportsStrictJson, "Strict JSON support should be exposed.");
                        Equal(4, summary.MaxConcurrentRequests, "Summary should expose concurrency limit.");
                        Contains(json, "Native tools supported.", "Capability note should serialize.");
                        Contains(json, "\"MaxConcurrentRequests\":4", "Provider summary should serialize concurrency limit.");
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
                                    Phase = "native",
                                    Arguments = "{\"Query\":\"SELECT COUNT(*) FROM users\"}",
                                    Result = "{\"RowsReturned\":1}",
                                    Success = true,
                                    TotalMs = 12
                                }
                            },
                            ExecutionPath = "native_tool_calls",
                            CapabilityNotice = "Native tool calls are enabled."
                        };

                        string json = Serializer.SerializeJson(result, false);
                        Contains(json, "\"TimeToFirstTokenMs\":10", "TTFT should serialize.");
                        Contains(json, "\"TotalStreamingTimeMs\":125", "Total time should serialize.");
                        Contains(json, "\"InputTokens\":100", "Input tokens should serialize.");
                        Contains(json, "\"OutputTokens\":20", "Output tokens should serialize.");
                        Contains(json, "\"TotalTokens\":120", "Total tokens should serialize.");
                        Contains(json, "\"ToolCalls\"", "Tool calls should serialize.");
                        Contains(json, "\"ExecutionPath\":\"native_tool_calls\"", "Execution path should serialize.");
                        Contains(json, "\"CapabilityNotice\":\"Native tool calls are enabled.\"", "Capability notice should serialize.");
                        Contains(json, "\"Phase\":\"native\"", "Tool phase should serialize.");
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
                    Case("ModelGuards", "BuildTableContextModelsSerialize", "Build table context request and response serialize", ct =>
                    {
                        BuildTableContextRequest request = new BuildTableContextRequest
                        {
                            ProviderId = "provider_ollama_local",
                            Prompt = "Build concise table context.",
                            TableIds = new List<string> { "tbl_sample_users" }
                        };

                        BuildTableContextResponse response = new BuildTableContextResponse
                        {
                            Success = true,
                            DatabaseId = "db_sample_sqlite",
                            ProviderId = "provider_ollama_local",
                            Model = "gemma3:4b",
                            Objects = new List<TableContextRead>
                            {
                                new TableContextRead
                                {
                                    DatabaseId = "db_sample_sqlite",
                                    TableId = "tbl_sample_users",
                                    TableName = "users",
                                    Context = "Generated table context."
                                }
                            },
                            Telemetry = new ChatTelemetry
                            {
                                InputTokens = 11,
                                OutputTokens = 21,
                                TotalTokens = 32
                            }
                        };

                        string requestJson = Serializer.SerializeJson(request, false);
                        string responseJson = Serializer.SerializeJson(response, false);
                        Contains(requestJson, "\"TableIds\":[\"tbl_sample_users\"]", "Table IDs should serialize.");
                        Contains(responseJson, "\"Objects\"", "Generated table contexts should serialize.");
                        Contains(responseJson, "\"Context\":\"Generated table context.\"", "Table context should serialize.");
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
                        await WithTempPersistenceAsync(async persistence =>
                        {
                            Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                            Equal(11, tools.Count, "Tool count mismatch.");
                            True(tools.ContainsKey("tablix_discover_databases"), "discover databases missing.");
                            True(tools.ContainsKey("tablix_discover_database"), "discover database missing.");
                            True(tools.ContainsKey("tablix_list_tables"), "list tables missing.");
                            True(tools.ContainsKey("tablix_discover_table"), "discover table missing.");
                            True(tools.ContainsKey("tablix_list_relationships"), "list relationships missing.");
                            True(tools.ContainsKey("tablix_execute_query"), "execute query missing.");
                            True(tools.ContainsKey("tablix_get_database_context"), "get database context missing.");
                            True(tools.ContainsKey("tablix_get_table_context"), "get table context missing.");
                            True(tools.ContainsKey("tablix_update_context"), "update context missing.");
                            True(tools.ContainsKey("tablix_update_database_context"), "update database context missing.");
                            True(tools.ContainsKey("tablix_update_table_context"), "update table context missing.");
                            await Task.CompletedTask.ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "DiscoverDatabasesPaginates", "MCP discover databases paginates", async ct =>
                    {
                        await WithTempPersistenceAsync(async persistence =>
                        {
                            await persistence.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "alpha_db", Name = "Alpha" }, ct).ConfigureAwait(false);
                            await persistence.DatabaseConnections.CreateAsync(new DatabaseEntry { Id = "beta_db", Name = "Beta" }, ct).ConfigureAwait(false);
                            Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                            EnumerationResult<DatabaseSummary> result = ConvertObject<EnumerationResult<DatabaseSummary>>(await tools["tablix_discover_databases"](new McpDiscoverDatabasesRequest { MaxResults = 2, Skip = 0 }).ConfigureAwait(false));
                            Equal(2, result.Objects.Count, "Page count mismatch.");
                            False(result.EndOfResults, "EndOfResults should be false.");
                            Equal(2, result.NextSkip.Value, "NextSkip mismatch.");
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "DiscoverDatabasesRedactsCredentials", "MCP discover databases never returns credentials", async ct =>
                    {
                        await WithTempPersistenceAsync(async persistence =>
                        {
                            await persistence.DatabaseConnections.CreateAsync(new DatabaseEntry
                            {
                                Id = "secret_db",
                                Type = DatabaseTypeEnum.Postgresql,
                                Hostname = "pg.example.com",
                                User = "readonly_user",
                                Password = "plaintext-secret",
                                DatabaseName = "orders"
                            }, ct).ConfigureAwait(false);

                            Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                            object toolResult = await tools["tablix_discover_databases"](new McpDiscoverDatabasesRequest { Filter = "secret_db" }).ConfigureAwait(false);
                            string json = Serializer.SerializeJson(toolResult, false);
                            EnumerationResult<DatabaseSummary> result = ConvertObject<EnumerationResult<DatabaseSummary>>(toolResult);
                            DatabaseSummary first = result.Objects[0];
                            True(first.HasUser, "HasUser should be returned.");
                            True(first.HasPassword, "HasPassword should be returned.");
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
                            await WithTempPersistenceAsync(async persistence =>
                            {
                                await ConfigureOnlyDatabaseAsync(persistence, entry, ct).ConfigureAwait(false);
                                Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                                DatabaseTableListResult result = ConvertObject<DatabaseTableListResult>(await tools["tablix_list_tables"](new McpListTablesRequest { DatabaseId = "test_sqlite", MaxResults = 2, Skip = 0 }).ConfigureAwait(false));
                                Equal(2, result.Objects.Count, "Table page count mismatch.");
                                Equal(3, result.TableCount, "TableCount mismatch.");
                                False(result.EndOfResults, "EndOfResults should be false.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "DiscoverTableReturnsGeometry", "MCP discover table returns table geometry", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            await WithTempPersistenceAsync(async persistence =>
                            {
                                await ConfigureOnlyDatabaseAsync(persistence, entry, ct).ConfigureAwait(false);
                                Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                                McpTableDetailResponse result = ConvertObject<McpTableDetailResponse>(await tools["tablix_discover_table"](new McpDiscoverTableRequest { DatabaseId = "test_sqlite", TableName = "users" }).ConfigureAwait(false));
                                Equal("users", result.Table.TableName, "TableName mismatch.");
                                Equal(4, result.Table.Columns.Count, "Column count mismatch.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "ListRelationshipsReturnsForeignKeys", "MCP list relationships returns sample FKs", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            await WithTempPersistenceAsync(async persistence =>
                            {
                                await ConfigureOnlyDatabaseAsync(persistence, entry, ct).ConfigureAwait(false);
                                Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                                DatabaseRelationshipListResult result = ConvertObject<DatabaseRelationshipListResult>(await tools["tablix_list_relationships"](new McpListRelationshipsRequest { DatabaseId = "test_sqlite", MaxResults = 10 }).ConfigureAwait(false));
                                True(result.Objects.Any(relationship => relationship.ToTable == "users"), "Expected relationship to users.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "UpdateContextReplaceAndAppend", "MCP update context supports replace and append", async ct =>
                    {
                        await WithTempPersistenceAsync(async persistence =>
                        {
                            Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                            McpContextUpdateResponse replace = ConvertObject<McpContextUpdateResponse>(await tools["tablix_update_context"](new McpUpdateContextRequest { DatabaseId = "db_sample_sqlite", Context = "First", Mode = "replace" }).ConfigureAwait(false));
                            True(replace.Success, "Replace should succeed.");

                            McpContextUpdateResponse append = ConvertObject<McpContextUpdateResponse>(await tools["tablix_update_context"](new McpUpdateContextRequest { DatabaseId = "db_sample_sqlite", Context = "Second", Mode = "append" }).ConfigureAwait(false));
                            True(append.Success, "Append should succeed.");
                            DatabaseEntry database = await persistence.DatabaseConnections.ReadAsync("db_sample_sqlite", ct).ConfigureAwait(false);
                            string context = database.Context;
                            Contains(context, "First", "Context should contain original text.");
                            Contains(context, "Second", "Context should contain appended text.");
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "GetDatabaseContextSupportsSingleAndMultiple", "MCP database context can be read for one or multiple databases", async ct =>
                    {
                        await WithTempPersistenceAsync(async persistence =>
                        {
                            Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                            await tools["tablix_update_database_context"](new McpUpdateContextRequest { DatabaseId = "db_sample_sqlite", Context = "Sample context", Mode = "replace" }).ConfigureAwait(false);

                            McpDatabaseContextReadResponse single = ConvertObject<McpDatabaseContextReadResponse>(await tools["tablix_get_database_context"](new McpGetDatabaseContextRequest { DatabaseId = "db_sample_sqlite" }).ConfigureAwait(false));
                            True(single.Success, "Single database context read should succeed.");
                            Equal(1L, single.TotalRecords, "Single read count mismatch.");
                            Contains(single.Objects[0].Context, "Sample context", "Single read should return saved context.");

                            McpDatabaseContextReadResponse multiple = ConvertObject<McpDatabaseContextReadResponse>(await tools["tablix_get_database_context"](new McpGetDatabaseContextRequest { DatabaseIds = new List<string> { "db_sample_sqlite", "missing_db" } }).ConfigureAwait(false));
                            Equal(1, multiple.Objects.Count, "Multiple read should return existing database.");
                            Equal(1, multiple.MissingDatabaseIds.Count, "Multiple read should report missing database.");
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "TableContextReadWriteSupportsSingleAndBatch", "MCP table context can be read and written for one or multiple tables", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            await WithTempPersistenceAsync(async persistence =>
                            {
                                await ConfigureOnlyDatabaseAsync(persistence, entry, ct).ConfigureAwait(false);
                                Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                                DatabaseTableListResult tables = ConvertObject<DatabaseTableListResult>(await tools["tablix_list_tables"](new McpListTablesRequest { DatabaseId = "test_sqlite", MaxResults = 10 }).ConfigureAwait(false));
                                TableSummary users = tables.Objects.First(table => table.TableName == "users");
                                TableSummary orders = tables.Objects.First(table => table.TableName == "orders");

                                McpContextUpdateResponse singleUpdate = ConvertObject<McpContextUpdateResponse>(await tools["tablix_update_table_context"](new McpUpdateContextRequest
                                {
                                    DatabaseId = "test_sqlite",
                                    TableId = users.TableId,
                                    Context = "Users table context",
                                    Mode = "replace"
                                }).ConfigureAwait(false));
                                True(singleUpdate.Success, "Single table context update should succeed.");
                                Equal(users.TableId, singleUpdate.TableId, "Updated table ID mismatch.");

                                McpTableContextReadResponse singleRead = ConvertObject<McpTableContextReadResponse>(await tools["tablix_get_table_context"](new McpGetTableContextRequest
                                {
                                    DatabaseId = "test_sqlite",
                                    TableId = users.TableId
                                }).ConfigureAwait(false));
                                True(singleRead.Success, "Single table context read should succeed.");
                                Contains(singleRead.Objects[0].Context, "Users table context", "Single table context should be returned.");

                                McpContextUpdateResponse batchUpdate = ConvertObject<McpContextUpdateResponse>(await tools["tablix_update_table_context"](new McpUpdateContextRequest
                                {
                                    DatabaseId = "test_sqlite",
                                    Updates = new List<McpContextUpdateItemRequest>
                                    {
                                        new McpContextUpdateItemRequest { TableId = users.TableId, Context = "Users append", Mode = "append" },
                                        new McpContextUpdateItemRequest { TableId = orders.TableId, Context = "Orders table context", Mode = "replace" }
                                    }
                                }).ConfigureAwait(false));
                                True(batchUpdate.Success, "Batch table context update should succeed.");
                                Equal(2, batchUpdate.Succeeded, "Batch success count mismatch.");

                                McpTableContextReadResponse batchRead = ConvertObject<McpTableContextReadResponse>(await tools["tablix_get_table_context"](new McpGetTableContextRequest
                                {
                                    DatabaseId = "test_sqlite",
                                    TableIds = new List<string> { users.TableId, orders.TableId }
                                }).ConfigureAwait(false));
                                Equal(2, batchRead.Objects.Count, "Batch table context read count mismatch.");
                                True(batchRead.Objects.Any(context => context.TableId == users.TableId && context.Context.Contains("Users append", StringComparison.OrdinalIgnoreCase)), "Appended users context should be returned.");
                                True(batchRead.Objects.Any(context => context.TableId == orders.TableId && context.Context.Contains("Orders table context", StringComparison.OrdinalIgnoreCase)), "Orders context should be returned.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "GenericContextUpdateUsesScopeDiscriminator", "MCP generic context update supports table scope discriminator", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            await WithTempPersistenceAsync(async persistence =>
                            {
                                await ConfigureOnlyDatabaseAsync(persistence, entry, ct).ConfigureAwait(false);
                                Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                                DatabaseTableListResult tables = ConvertObject<DatabaseTableListResult>(await tools["tablix_list_tables"](new McpListTablesRequest { DatabaseId = "test_sqlite", MaxResults = 10 }).ConfigureAwait(false));
                                TableSummary users = tables.Objects.First(table => table.TableName == "users");

                                McpContextUpdateResponse update = ConvertObject<McpContextUpdateResponse>(await tools["tablix_update_context"](new McpUpdateContextRequest
                                {
                                    Scope = ContextScopeEnum.Table,
                                    DatabaseId = "test_sqlite",
                                    TableId = users.TableId,
                                    Context = "Generic table context",
                                    Mode = "replace"
                                }).ConfigureAwait(false));

                                True(update.Success, "Generic scoped table update should succeed.");
                                Equal(ContextScopeEnum.Table, update.Scope, "Scope mismatch.");
                                Equal(users.TableId, update.TableId, "Table ID mismatch.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "ExecuteQueryRespectsAllowedQueries", "MCP execute query rejects disallowed statement type", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            entry.AllowedQueries = new List<string> { "SELECT" };
                            await WithTempPersistenceAsync(async persistence =>
                            {
                                await ConfigureOnlyDatabaseAsync(persistence, entry, ct).ConfigureAwait(false);
                                Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                                QueryResult result = ConvertObject<QueryResult>(await tools["tablix_execute_query"](new McpExecuteQueryRequest { DatabaseId = "test_sqlite", Query = "DELETE FROM users" }).ConfigureAwait(false));
                                False(result.Success, "DELETE should be rejected.");
                                Contains(result.Error, "DELETE", "Error should mention DELETE.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "ExecuteQueryReturnsRows", "MCP execute query returns actual result rows", async ct =>
                    {
                        await WithTempDatabaseAsync(async entry =>
                        {
                            entry.AllowedQueries = new List<string> { "SELECT" };
                            await WithTempPersistenceAsync(async persistence =>
                            {
                                await ConfigureOnlyDatabaseAsync(persistence, entry, ct).ConfigureAwait(false);
                                Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                                QueryResult result = ConvertObject<QueryResult>(await tools["tablix_execute_query"](new McpExecuteQueryRequest { DatabaseId = "test_sqlite", Query = "SELECT COUNT(*) AS total_users FROM users" }).ConfigureAwait(false));
                                string resultJson = Serializer.SerializeJson(result, false);
                                True(result.Success, "SELECT count should succeed.");
                                Equal(1, result.RowsReturned, "Count query should return one row.");
                                Contains(resultJson, "\"total_users\":5", "Sample users count mismatch.");
                            }).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                    }),
                    Case("McpToolBehavior", "MissingRequiredArgumentsReturnErrors", "MCP tools return useful errors for missing required arguments", async ct =>
                    {
                        await WithTempPersistenceAsync(async persistence =>
                        {
                            Dictionary<string, Func<object, Task<object>>> tools = RegisteredTools(persistence, new CrawlCache());
                            McpErrorResponse tableResult = ConvertObject<McpErrorResponse>(await tools["tablix_discover_table"](new McpDiscoverTableRequest { DatabaseId = "db_sample_sqlite" }).ConfigureAwait(false));
                            Contains(tableResult.Error, "tableName", "Missing tableName should be reported.");

                            QueryResult queryResult = ConvertObject<QueryResult>(await tools["tablix_execute_query"](new McpExecuteQueryRequest { DatabaseId = "db_sample_sqlite" }).ConfigureAwait(false));
                            False(queryResult.Success, "Missing query should fail.");
                            Contains(queryResult.Error, "query", "Missing query should be reported.");
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
                        Contains(descriptions["tablix_execute_query"], "update database context", "Query tool should direct agents to correct stale database context.");
                        Contains(descriptions["tablix_execute_query"], "update that table context", "Query tool should direct agents to correct stale table context.");
                        return Task.CompletedTask;
                    }),
                    Case("McpGuidance", "ContextGuidance", "Context update guidance protects persisted context quality", ct =>
                    {
                        Dictionary<string, string> descriptions = RegisteredToolDescriptions();
                        Contains(descriptions["tablix_get_database_context"], "one database, multiple databases", "Database context read should describe single and multi-entity reads.");
                        Contains(descriptions["tablix_get_database_context"], "Context is guidance, not proof", "Database context read should require schema verification.");
                        Contains(descriptions["tablix_get_table_context"], "one table, multiple tables", "Table context read should describe single and multi-entity reads.");
                        Contains(descriptions["tablix_get_table_context"], "includeEmpty", "Table context read should explain empty table contexts.");
                        Contains(descriptions["tablix_get_table_context"], "tablix_discover_table", "Table context read should not replace schema discovery.");
                        Contains(descriptions["tablix_update_database_context"], "database-level context", "Database context update alias should be explicit.");
                        Contains(descriptions["tablix_update_table_context"], "table-level context", "Table context update alias should be explicit.");
                        Contains(descriptions["tablix_update_context"], "scope = Database or Table", "Generic context update should document the scope discriminator.");
                        Contains(descriptions["tablix_update_context"], "Prefer tablix_update_database_context", "Generic context update should point to explicit aliases.");
                        Contains(descriptions["tablix_update_context"], "user asks to save/update context", "Context update should not be casual.");
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
                    Case("DashboardApiContract", "OpenApiDocumentsProductRouteGroups", "OpenAPI documents all product route groups", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string serverRoutes = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "TablixServer.cs"));

                        Contains(serverRoutes, "new OpenApiTag(\"Database\"", "OpenAPI should include Database tag.");
                        Contains(serverRoutes, "new OpenApiTag(\"Metadata\"", "OpenAPI should include Metadata tag.");
                        Contains(serverRoutes, "new OpenApiTag(\"Models\"", "OpenAPI should include Models tag.");
                        Contains(serverRoutes, "new OpenApiTag(\"Context\"", "OpenAPI should include Context tag.");
                        Contains(serverRoutes, "new OpenApiTag(\"Setup\"", "OpenAPI should include Setup tag.");
                        Contains(serverRoutes, "new OpenApiTag(\"Chat\"", "OpenAPI should include Chat tag.");
                        Contains(serverRoutes, "new OpenApiTag(\"Settings\"", "OpenAPI should include Settings tag.");
                        Contains(serverRoutes, "new OpenApiTag(\"Health\"", "OpenAPI should include Health tag.");
                        Contains(serverRoutes, "OpenApiResponseMetadata.Json<BuildTableContextResponse>", "OpenAPI should document generated table context responses.");
                        Contains(serverRoutes, "OpenApiRequestBodyMetadata.Json<BuildTableContextRequest>", "OpenAPI should document generated table context requests.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "TableContextBuildValidatesPersistedTableIds", "Table context build validates persisted table identifiers", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));
                        string tableContextMethods = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Core", "Persistence", "Sqlite", "Implementations", "SqliteTableContextMethods.cs"));

                        Contains(chatHandler, "BuildMissingTableIds", "Table context build should detect requested table IDs missing from persisted metadata.");
                        Contains(chatHandler, "not found in persisted crawl metadata", "Table context build should return a clear persisted-metadata error.");
                        Contains(chatHandler, "ReadDetailAsync(database.Id", "Context build should prefer persisted crawl metadata over in-memory cache.");
                        Contains(chatHandler, "catch (KeyNotFoundException", "Table context build should convert persistence missing-table errors to API errors.");
                        Contains(tableContextMethods, "TableExistsAsync", "Table context writes should verify persisted table metadata before insert.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "PersistedMetadataPreferredOverCrawlCache", "Dashboard and chat reads prefer persisted metadata over stale crawl cache", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string databaseHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "DatabaseHandler.cs"));
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));

                        Contains(databaseHandler, "DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(databaseId, token)", "Database detail reads should load persisted metadata before crawl cache.");
                        Contains(databaseHandler, "return _CrawlCache.Get(databaseId);", "Database detail reads should use crawl cache only as a fallback.");
                        Contains(databaseHandler, "DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(entry.Id)", "Table and relationship reads should prefer persisted crawl metadata.");
                        Contains(chatHandler, "DatabaseDetail detail = await _Persistence.DatabaseMetadata.ReadDetailAsync(database.Id, req.CancellationToken)", "Chat and context build paths should prefer persisted metadata with saved context records.");
                        Contains(chatHandler, "detail = _CrawlCache.Get(database.Id);", "Chat and context build paths should use crawl cache only as fallback.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "TableContextGenerationUsesProviderConcurrency", "Table context generation uses bounded provider concurrency", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string nginx = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "nginx.conf"));
                        string entrypoint = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "entrypoint.sh"));

                        Contains(chatHandler, "preparation.Provider.MaxConcurrentRequests", "Backend batch generation should use provider concurrency limit.");
                        Contains(chatHandler, "GenerateOneTableContextAsync", "Backend batch generation should issue per-table provider requests.");
                        Contains(setupWizard, "buildTableContextsWithConcurrency", "Setup wizard should build table contexts through bounded per-table calls.");
                        Contains(setupWizard, "/table-context/${tableId}/build", "Setup wizard should call per-table build endpoints instead of one long batch.");
                        DoesNotContain(setupWizard, "/table-context/build", "Setup wizard should not call the batch table-context build endpoint.");
                        Contains(setupWizard, "const received: Record<string, string> = {}", "Setup wizard should collect each completed table-context response independently.");
                        Contains(setupWizard, "setTableContexts(previous => ({ ...previous, ...received }))", "Setup wizard should update table context editors as each response arrives.");
                        Contains(setupWizard, "readJsonResponse", "Setup wizard should handle non-JSON gateway errors cleanly.");
                        Contains(nginx, "proxy_read_timeout 3600s;", "Dashboard nginx proxy should allow long-running API operations.");
                        Contains(nginx, "proxy_send_timeout 3600s;", "Dashboard nginx proxy should allow long-running API operations.");
                        Contains(entrypoint, "proxy_read_timeout 3600s;", "Dashboard runtime nginx proxy should allow long-running API operations.");
                        Contains(entrypoint, "proxy_send_timeout 3600s;", "Dashboard runtime nginx proxy should allow long-running API operations.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ProviderConcurrencyDocumented", "Provider concurrency is documented in REST, README, and Postman", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
                        string restApi = File.ReadAllText(Path.Combine(repositoryRoot, "REST_API.md"));
                        string postman = File.ReadAllText(Path.Combine(repositoryRoot, "Tablix.postman_collection.json"));

                        Contains(readme, "MaxConcurrentRequests", "README should document provider concurrency.");
                        Contains(restApi, "MaxConcurrentRequests", "REST API should document provider concurrency.");
                        Contains(restApi, "per provider request", "REST API should explain per-request timeout behavior.");
                        Contains(postman, "MaxConcurrentRequests", "Postman provider examples should include provider concurrency.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ReleaseVersion030Documented", "Release version 0.3.0 is reflected in docs, compose tags, package metadata, and product constants", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
                        string restApi = File.ReadAllText(Path.Combine(repositoryRoot, "REST_API.md"));
                        string mcpApi = File.ReadAllText(Path.Combine(repositoryRoot, "MCP_API.md"));
                        string gettingStarted = File.ReadAllText(Path.Combine(repositoryRoot, "GETTING_STARTED.md"));
                        string changelog = File.ReadAllText(Path.Combine(repositoryRoot, "CHANGELOG.md"));
                        string compose = File.ReadAllText(Path.Combine(repositoryRoot, "docker", "compose.yaml"));
                        string buildAll = File.ReadAllText(Path.Combine(repositoryRoot, "build-all.bat"));
                        string buildServer = File.ReadAllText(Path.Combine(repositoryRoot, "build-server.bat"));
                        string buildDashboard = File.ReadAllText(Path.Combine(repositoryRoot, "build-dashboard.bat"));
                        string serverDockerfile = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Dockerfile"));
                        string constants = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Core", "Helpers", "Constants.cs"));
                        string oldCloudBuilderName = "cloud-" + "jchristn77-" + "jchristn77";
                        string builderArgument = "-" + "-builder";
                        List<string> projectFiles = Directory
                            .GetFiles(Path.Combine(repositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
                            .OrderBy(filename => filename)
                            .ToList();

                        Contains(readme, "<b>v0.3.0 - ALPHA</b>", "README should show the current release tag.");
                        Contains(readme, "## What's New in v0.3.0", "README current release section should be v0.3.0.");
                        Contains(readme, "jchristn77/tablix-server:v0.3.0", "README server image examples should use v0.3.0.");
                        Contains(readme, "jchristn77/tablix-ui:v0.3.0", "README UI image examples should use v0.3.0.");
                        Contains(readme, "build-all.bat v0.3.0", "README build instructions should use v0.3.0.");
                        DoesNotContain(readme, oldCloudBuilderName, "README build instructions should not require a hard-coded cloud builder.");
                        DoesNotContain(readme, "v0.2.0", "README current-facing release references should not stay on v0.2.0.");

                        Contains(restApi, "\"Version\": \"0.3.0\"", "REST API health example should use product version 0.3.0.");
                        Contains(restApi, "tablix_update_database_context", "REST API chat docs should mention database context update tools.");
                        Contains(restApi, "tablix_update_table_context", "REST API chat docs should mention table context update tools.");
                        Contains(restApi, "Chat.Tools.AllowContextUpdates", "REST API chat docs should document the context-update gate.");
                        Contains(mcpApi, "REST chat context updates", "MCP API docs should connect MCP and REST chat context updates.");
                        Contains(gettingStarted, "Database and table context updates", "Getting started chat docs should mention context update tool calls.");
                        Contains(changelog, "## v0.3.0 - ALPHA", "Changelog should include the v0.3.0 release.");

                        Contains(compose, "jchristn77/tablix-server:v0.3.0", "Compose server image tag should be v0.3.0.");
                        Contains(compose, "jchristn77/tablix-ui:v0.3.0", "Compose UI image tag should be v0.3.0.");
                        DoesNotContain(compose, "v0.2.0", "Compose image tags should not remain on v0.2.0.");
                        Contains(buildAll, "Example: build-all.bat v0.3.0", "Build-all script example should use v0.3.0.");
                        Contains(buildServer, "Example: build-server.bat v0.3.0", "Build-server script example should use v0.3.0.");
                        Contains(buildDashboard, "Example: build-dashboard.bat v0.3.0", "Build-dashboard script example should use v0.3.0.");
                        DoesNotContain(buildServer, oldCloudBuilderName, "Build-server should not hard-code a Docker cloud builder.");
                        DoesNotContain(buildDashboard, oldCloudBuilderName, "Build-dashboard should not hard-code a Docker cloud builder.");
                        DoesNotContain(buildServer, builderArgument, "Build-server should use the active Docker Buildx builder.");
                        DoesNotContain(buildDashboard, builderArgument, "Build-dashboard should use the active Docker Buildx builder.");
                        Contains(buildServer, "if errorlevel 1 (", "Build-server should stop when docker buildx fails.");
                        Contains(buildDashboard, "if errorlevel 1 (", "Build-dashboard should stop when docker buildx fails.");
                        Contains(buildServer, "exit /b %errorlevel%", "Build-server should propagate docker buildx failures.");
                        Contains(buildDashboard, "exit /b %errorlevel%", "Build-dashboard should propagate docker buildx failures.");
                        Contains(serverDockerfile, "https://security.ubuntu.com/ubuntu", "Server Dockerfile should use HTTPS Ubuntu security sources.");
                        Contains(serverDockerfile, "Acquire::Retries \"5\";", "Server Dockerfile should retry transient apt fetch failures.");
                        Contains(serverDockerfile, "Acquire::https::Timeout \"30\";", "Server Dockerfile should set an apt HTTPS timeout.");
                        foreach (string projectFile in projectFiles)
                        {
                            string project = File.ReadAllText(projectFile);
                            Contains(project, "<Version>0.3.0</Version>", Path.GetFileName(projectFile) + " package version should be 0.3.0.");
                        }
                        Contains(constants, "ProductVersion = \"0.3.0\"", "Runtime product version should be 0.3.0.");
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
                    Case("DashboardApiContract", "ChatEmptyStateIsCentered", "Chat empty state is centered in the transcript", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ChatPage.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(chatPage, "className=\"chat-empty\"", "Chat page should render a dedicated empty-state container.");
                        Contains(chatPage, "Ask about the selected database.", "Chat empty state should include the primary instruction.");
                        Contains(chatPage, "Responses can include markdown, SQL, tables, and lists.", "Chat empty state should include the response capability note.");
                        Contains(stylesheet, ".chat-transcript-content", "Chat transcript content should provide the centering container.");
                        Contains(stylesheet, "min-height: 100%;", "Chat transcript content should fill the available transcript height.");
                        Contains(stylesheet, ".chat-empty", "Chat empty state should have dedicated styling.");
                        Contains(stylesheet, "margin: auto;", "Chat empty state should center vertically and horizontally inside the transcript.");
                        DoesNotContain(stylesheet, "margin: 80px auto;", "Chat empty state should not use a fixed top offset.");
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
                    Case("DashboardApiContract", "ChatDefaultProviderIgnoresStaleSettings", "Chat default provider ignores stale settings values", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));
                        string modelProviderHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ModelProviderHandler.cs"));
                        string tablixServer = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "TablixServer.cs"));
                        string chatPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ChatPage.tsx"));
                        string databaseListPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "DatabaseListPage.tsx"));
                        string databaseDetailPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "DatabaseDetailPage.tsx"));

                        Contains(chatHandler, "SelectEffectiveDefaultProviderId(settings.Chat.DefaultProviderId, providers)", "Chat options should return an enabled provider as the effective default.");
                        Contains(chatHandler, "ResolveProviderAsync(providerId, settings.Chat.DefaultProviderId", "Chat handlers should resolve stale configured defaults to an enabled provider.");
                        Contains(chatHandler, "selectedIsConfiguredDefault", "Provider fallback should be limited to the configured default path.");
                        Contains(modelProviderHandler, "RepairDefaultProviderIdAsync", "Model provider changes should repair stale default provider settings.");
                        Contains(modelProviderHandler, "settings.Chat.DefaultProviderId = replacementProviderId", "Default provider repair should persist the replacement provider ID.");
                        Contains(modelProviderHandler, "ResolveProviderForTestAsync", "Model provider tests should resolve stored secrets for redacted edit forms.");
                        Contains(modelProviderHandler, "provider.ApiKey = existing.ApiKey", "Model provider draft tests should reuse the stored API key when the edit form leaves it blank.");
                        Contains(tablixServer, "new ModelProviderHandler(_SettingsManager, _Persistence, _Logging)", "Model provider handler should have settings access for default repair.");
                        Contains(chatPage, "function selectAvailableProviderId", "Chat page should validate default provider IDs against returned providers.");
                        Contains(databaseListPage, "function selectAvailableProviderId", "Database list context builder should validate default provider IDs.");
                        Contains(databaseDetailPage, "function selectAvailableProviderId", "Database detail context builder should validate default provider IDs.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ChatExecutionPathVisible", "Chat page displays tool execution path and capability notices", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ChatPage.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(chatPage, "chat-capability-notice", "Chat page should show fallback provider capability notice.");
                        Contains(chatPage, "This provider is not configured for native tool calls", "Chat page should explain fallback execution when native tools are not active.");
                        DoesNotContain(chatPage, "Native tool calls are enabled for this provider. Tablix validates every database query before execution.", "Chat page should not repeat native tool status already shown in the provider line.");
                        DoesNotContain(chatPage, "No tool call requested", "Chat page should not render the old native_no_tool_call label.");
                        Contains(chatPage, "chat-execution-note", "Chat page should show response execution notes.");
                        Contains(chatPage, "ExecutionPath", "Chat page should consume execution path.");
                        Contains(chatPage, "The model did not request a tool call.", "Chat page should render native_no_tool_call as a complete user-facing sentence.");
                        Contains(chatPage, "Tool execution failed; the assistant returned a plain model response.", "Chat page should render native_failed_plain as a user-facing sentence.");
                        Contains(chatPage, "Tablix could not plan a database query for this request.", "Chat page should render fallback planner failures as user-facing text.");
                        Contains(chatPage, "function formatCapabilityNotice", "Chat page should normalize capability notices before display.");
                        Contains(chatPage, "return null;", "Chat page should suppress redundant provider capability notices.");
                        Contains(chatPage, "CapabilityNotice", "Chat page should consume capability notice.");
                        Contains(stylesheet, ".chat-capability-notice", "Capability notice should be styled.");
                        Contains(stylesheet, ".chat-execution-note", "Execution note should be styled.");
                        DoesNotContain(stylesheet, "text-transform: capitalize;", "Chat execution notes should preserve sentence casing.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ChatStreamingUsesPolyPromptChunks", "Streaming chat sends PolyPrompt chunks instead of one completed message", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));

                        Contains(chatHandler, "ExecuteChatResponseStreamingAsync", "SSE chat should use a dedicated streaming execution path.");
                        Contains(chatHandler, "ChatStreamingAsync", "SSE chat should call PolyPrompt streaming APIs.");
                        Contains(chatHandler, "await foreach (ChatStreamingChunk chunk", "SSE chat should enumerate provider chunks.");
                        Contains(chatHandler, "Delta = chunk.Text", "SSE token events should send individual chunk text.");
                        Contains(chatHandler, "BuildToolFollowupPrompt(preparation.Prompt, executedTools)", "Native tool execution should stream the final post-tool answer.");
                        Contains(chatHandler, "ExecuteFallbackPlanningStreamingAsync", "Fallback tool execution should stream the final post-tool answer.");
                        Contains(chatHandler, "Message = response.Text ?? String.Empty", "Native no-tool streaming should return the model response without pre-streaming a plain fallback candidate.");
                        DoesNotContain(chatHandler, "return await ExecutePlainChatStreamingAsync(client, preparation, \"native_no_tool_call\"", "Native no-tool streaming should not stream a plain response before server fallback can run.");
                        DoesNotContain(chatHandler, "Delta = execution.Message", "SSE chat should not send the full completed answer as one token event.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ChatIntentUsesModelPlanning", "Chat fallback intent is decided by model planning rather than substring matching", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));
                        string executionPolicy = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatExecutionPolicy.cs"));
                        string fallbackPlan = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "FallbackQueryPlan.cs"));

                        Contains(chatHandler, "Classify the latest user message", "Fallback planner should classify the latest user intent.");
                        Contains(chatHandler, "Intent must be one of", "Fallback planner should use explicit intent labels.");
                        Contains(chatHandler, "Execute to false", "Fallback planner should allow no-query decisions.");
                        Contains(executionPolicy, "UseFallbackPlanner", "Execution policy should route to model-based fallback planning.");
                        Contains(fallbackPlan, "PromptIntentTypeEnum Intent", "Fallback query plan should preserve the model-classified intent.");
                        DoesNotContain(chatHandler, "user.Contains(", "Chat intent routing should not use substring matching against user prompts.");
                        DoesNotContain(chatHandler, "UserAskedForData", "Chat intent routing should not keep the old data-request heuristic.");
                        DoesNotContain(chatHandler, "UserAskedOnlyForSql", "Chat intent routing should not keep the old SQL-only heuristic.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ChatContextUpdateToolsAreExposed", "Chat exposes durable context update tools with typed arguments and exact routing", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));
                        string toolDefinitions = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "TablixChatToolDefinitions.cs"));
                        string databaseArguments = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "TablixUpdateDatabaseContextArguments.cs"));
                        string tableArguments = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "TablixUpdateTableContextArguments.cs"));
                        string updateResult = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatContextUpdateToolResult.cs"));
                        string enabledTools = Serializer.SerializeJson(TablixChatToolDefinitions.Build(true), false);
                        string disabledTools = Serializer.SerializeJson(TablixChatToolDefinitions.Build(false), false);

                        Contains(enabledTools, "tablix_execute_query", "Native chat should continue exposing the query execution tool.");
                        Contains(enabledTools, "tablix_update_database_context", "Native chat should expose a database context update tool.");
                        Contains(enabledTools, "tablix_update_table_context", "Native chat should expose a table context update tool.");
                        DoesNotContain(disabledTools, "tablix_update_database_context", "Database context updates should not be exposed when disabled.");
                        DoesNotContain(disabledTools, "tablix_update_table_context", "Table context updates should not be exposed when disabled.");

                        Contains(toolDefinitions, "UpdateDatabaseContextToolName = \"tablix_update_database_context\"", "Database context tool name should be a constant.");
                        Contains(toolDefinitions, "UpdateTableContextToolName = \"tablix_update_table_context\"", "Table context tool name should be a constant.");
                        Contains(toolDefinitions, "BuildUpdateDatabaseContextTool()", "Database context tool definition should be explicit.");
                        Contains(toolDefinitions, "BuildUpdateTableContextTool()", "Table context tool definition should be explicit.");
                        Contains(toolDefinitions, "Use append for incremental observations", "Context tool descriptions should prefer append for new observations.");
                        Contains(toolDefinitions, "Do not store secrets", "Context tool descriptions should protect persisted context.");
                        Contains(toolDefinitions, "raw result rows", "Context tool descriptions should reject raw result persistence.");
                        Contains(toolDefinitions, "Clearly label inferred relationships", "Context tool descriptions should preserve inferred relationship provenance.");

                        Contains(chatHandler, "Tools = TablixChatToolDefinitions.Build(preparation.Settings.Chat.Tools.AllowContextUpdates)", "Native tool exposure should honor AllowContextUpdates.");
                        Contains(chatHandler, "ExecuteDatabaseContextUpdateToolCallAsync", "Chat handler should execute database context tool calls.");
                        Contains(chatHandler, "ExecuteTableContextUpdateToolCallAsync", "Chat handler should execute table context tool calls.");
                        Contains(chatHandler, "TablixUpdateDatabaseContextArguments arguments;", "Database context tool should deserialize to a named arguments type.");
                        Contains(chatHandler, "TablixUpdateTableContextArguments arguments;", "Table context tool should deserialize to a named arguments type.");
                        Contains(chatHandler, "ChatContextUpdateToolResult result = new ChatContextUpdateToolResult", "Context update tool results should use a named result type.");
                        Contains(chatHandler, "_Persistence.DatabaseContexts.UpsertAsync(", "Database context tool should persist through database context storage.");
                        Contains(chatHandler, "_Persistence.TableContexts.UpsertAsync(", "Table context tool should persist through table context storage.");
                        Contains(chatHandler, "\"chat\",", "Chat context updates should be source-labelled as chat updates.");
                        Contains(chatHandler, "NormalizeContextUpdateMode(arguments.Mode, out error)", "Context update mode should be normalized and validated.");
                        Contains(chatHandler, "FindTablesByName(detail, arguments.SchemaName, arguments.TableName)", "Table context matching should use schema/table arguments.");
                        Contains(chatHandler, "Do not persist secrets, credentials, raw result rows", "Chat prompt should instruct the model not to persist sensitive or raw data.");

                        List<string> typedSources = new List<string>
                        {
                            chatHandler,
                            toolDefinitions,
                            databaseArguments,
                            tableArguments,
                            updateResult
                        };

                        foreach (string source in typedSources)
                        {
                            DoesNotContain(source, "JsonNode", "Chat tool path should not use JsonNode.");
                            DoesNotContain(source, "JsonElement", "Chat tool path should not use JsonElement.");
                            DoesNotContain(source, "JsonObject", "Chat tool path should not use JsonObject.");
                            DoesNotContain(source, "JsonArray", "Chat tool path should not use JsonArray.");
                            DoesNotContain(source, "var ", "Chat tool path should not use var.");
                            DoesNotContain(source, "Tuple<", "Chat tool path should not use Tuple.");
                            DoesNotContain(source, "ValueTuple", "Chat tool path should not use ValueTuple.");
                        }

                        DoesNotContain(chatHandler, ".Contains(", "Chat tool routing and context table matching should not use substring matching.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "ProviderPromptOverrideIsSurfaced", "Model provider prompt overrides are surfaced and documented in the dashboard", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string chatHandler = File.ReadAllText(Path.Combine(repositoryRoot, "src", "Tablix.Server", "Handlers", "ChatHandler.cs"));
                        string modelsPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ModelsPage.tsx"));
                        string readme = File.ReadAllText(Path.Combine(repositoryRoot, "README.md"));
                        string restApi = File.ReadAllText(Path.Combine(repositoryRoot, "REST_API.md"));

                        Contains(chatHandler, "BuildEffectiveSystemPrompt(settings, provider)", "Chat preparation should build one effective system prompt for every provider.");
                        Contains(chatHandler, "MandatoryExecutionSystemPrompt", "Provider prompt overrides should still receive mandatory execution and no-fabrication rules.");
                        Contains(chatHandler, "Never fabricate table contents", "Mandatory prompt rules should prohibit fabricated database facts.");
                        Contains(modelsPage, "System Prompt Override", "Models page should expose provider-specific system prompt override.");
                        Contains(modelsPage, "models.systemPrompt", "Provider prompt override should have localized tooltip coverage.");
                        Contains(readme, "provider-specific system prompt", "README should explain provider prompt overrides.");
                        Contains(restApi, "SystemPrompt", "REST API should document provider prompt override fields.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "DashboardTooltipsAreLocalized", "Dashboard control tooltips use selected-language-aware strings", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string navbar = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "Navbar.tsx"));
                        string modelsPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ModelsPage.tsx"));
                        string chatPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ChatPage.tsx"));
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string tooltipManager = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "LocalizedTooltipManager.tsx"));
                        string i18n = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "i18n.ts"));

                        Contains(navbar, "dashboardLanguages.map", "Navbar language selector should expose every supported language option.");
                        Contains(navbar, "translateTooltip('nav.databases', language)", "Navbar table of contents should use selected language tooltips.");
                        Contains(navbar, "className=\"language-select\"", "Dashboard should expose a selected-language control.");
                        Contains(modelsPage, "translateTooltip('models.systemPrompt')", "Model prompt override tooltip should be localized.");
                        Contains(chatPage, "translateTooltip('chat.streaming')", "Chat streaming control tooltip should be localized.");
                        Contains(setupWizard, "translateTooltip('models.concurrency')", "Setup wizard provider controls should use localized tooltips.");
                        Contains(tooltipManager, "querySelectorAll(controlSelector)", "Dashboard should apply fallback tooltips to all interactive controls.");
                        Contains(tooltipManager, "applyVisibleText(document.body, language)", "Dashboard should localize rendered static text, not only tooltips.");
                        Contains(tooltipManager, "applyAttributes(language)", "Dashboard should localize titles, placeholders, and aria labels.");
                        Contains(tooltipManager, "characterData: true", "Dashboard should relocalize dynamic text node changes.");
                        Contains(tooltipManager, "document.documentElement.dir = getLanguageDirection(language)", "Dashboard should update document direction for RTL languages.");
                        Contains(tooltipManager, "tablix-language-changed", "Dashboard tooltips should update when the selected language changes.");
                        Contains(i18n, "generic.button", "Tooltip localization should include fallback button help.");
                        Contains(i18n, "generic.input", "Tooltip localization should include fallback input help.");
                        Contains(i18n, "export type DashboardLanguage", "Tooltip localization should define supported dashboard languages.");
                        Contains(i18n, "'en' | 'es' | 'fr' | 'it' | 'pt' | 'zh' | 'yue' | 'ja-kanji' | 'ja' | 'fa'", "Dashboard localization should support all requested languages.");
                        Contains(i18n, "NativeLabel: 'Español'", "Dashboard localization should use readable native language labels.");
                        Contains(i18n, "NativeLabel: 'Français'", "Dashboard localization should include French.");
                        Contains(i18n, "NativeLabel: 'Italiano'", "Dashboard localization should include Italian.");
                        Contains(i18n, "NativeLabel: 'Português'", "Dashboard localization should include Portuguese.");
                        Contains(i18n, "NativeLabel: '普通话'", "Dashboard localization should include Mandarin.");
                        Contains(i18n, "NativeLabel: '廣東話'", "Dashboard localization should include Cantonese.");
                        Contains(i18n, "NativeLabel: '日本語（漢字）'", "Dashboard localization should include a Kanji-labeled Japanese option.");
                        Contains(i18n, "NativeLabel: '日本語'", "Dashboard localization should include Japanese.");
                        Contains(i18n, "NativeLabel: 'فارسی'", "Dashboard localization should include Farsi.");
                        Contains(i18n, "Direction: 'rtl'", "Dashboard localization should mark Farsi as RTL.");
                        Contains(i18n, "translateVisibleText", "Dashboard localization should expose visible text translation.");
                        Contains(i18n, "translateAttributeValue", "Dashboard localization should expose attribute translation.");
                        DoesNotContain(i18n, "generaciÃ", "Localized strings should not contain mojibake.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "TableViewsUseSharedActionMenus", "Dashboard table row actions use shared overflow menus above the workspace", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string databaseListPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "DatabaseListPage.tsx"));
                        string modelsPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ModelsPage.tsx"));
                        string actionMenu = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "ActionMenu.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(databaseListPage, "ActionMenu", "Databases table should use the shared action menu.");
                        Contains(modelsPage, "ActionMenu", "Models table should use the shared action menu.");
                        Contains(databaseListPage, "className=\"data-table wide-table\"", "Databases table should use wider table styling.");
                        Contains(modelsPage, "className=\"data-table wide-table\"", "Models table should use wider table styling.");
                        Contains(actionMenu, "className=\"floating-action-menu\"", "Action menu component should use the shared floating menu class.");
                        Contains(stylesheet, "position: fixed;", "Action menu should render relative to the viewport.");
                        Contains(stylesheet, "z-index: 950;", "Action menu should render above workspace content.");
                        Contains(stylesheet, ".wide-table", "Stylesheet should define wide table behavior.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "DashboardUsesCustomConfirmDialogs", "Dashboard destructive confirmations use custom modal dialogs", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string confirmDialog = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "ConfirmDialog.tsx"));
                        string databaseListPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "DatabaseListPage.tsx"));
                        string databaseDetailPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "DatabaseDetailPage.tsx"));
                        string modelsPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ModelsPage.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));
                        string dashboardSource = confirmDialog + databaseListPage + databaseDetailPage + modelsPage;

                        Contains(confirmDialog, "role=\"alertdialog\"", "ConfirmDialog should render an accessible alert dialog.");
                        Contains(confirmDialog, "Danger", "ConfirmDialog should support destructive action styling.");
                        Contains(databaseListPage, "<ConfirmDialog", "Database list delete should use the shared confirmation dialog.");
                        Contains(databaseDetailPage, "<ConfirmDialog", "Database detail delete should use the shared confirmation dialog.");
                        Contains(modelsPage, "<ConfirmDialog", "Models delete should use the shared confirmation dialog.");
                        Contains(stylesheet, ".confirm-dialog", "Confirm dialog should have dedicated modal sizing.");
                        DoesNotContain(dashboardSource, "confirm(", "Dashboard code should not use browser confirm dialogs.");
                        DoesNotContain(dashboardSource, "alert(", "Dashboard code should not use browser alert dialogs.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "SettingsExposePromptProcessing", "Settings page exposes prompt processing and native tool provider settings", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string settingsPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "SettingsPage.tsx"));
                        string modelsPage = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "pages", "ModelsPage.tsx"));
                        string types = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "types", "index.ts"));

                        Contains(settingsPage, "Prompt Processing", "Settings page should expose prompt processing section.");
                        Contains(settingsPage, "Prefer native tools", "Settings page should expose native tool preference.");
                        Contains(settingsPage, "Server fallback", "Settings page should expose fallback setting.");
                        Contains(modelsPage, "Supports native tools", "Models page should expose native tool support.");
                        Contains(modelsPage, "Use native tools", "Models page should expose native tool enablement.");
                        Contains(modelsPage, "Max Concurrent Requests", "Models page should expose provider concurrency limit.");
                        Contains(types, "PromptProcessingSettings", "TypeScript contracts should include prompt processing settings.");
                        Contains(types, "SupportsNativeToolCalls", "TypeScript contracts should include provider tool capability.");
                        Contains(types, "MaxConcurrentRequests", "TypeScript contracts should include provider concurrency limit.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "SetupWizardDatabaseFormAdaptsToType", "Setup wizard database form adapts to database type", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(setupWizard, "function updateDatabaseType", "Setup wizard should centralize database type changes.");
                        Contains(setupWizard, "database.Type === 'Sqlite'", "Setup wizard should render SQLite-specific fields separately.");
                        Contains(setupWizard, "Port: defaults.Port", "Setup wizard should reset the port to the selected database type default.");
                        Contains(setupWizard, "User: defaults.User", "Setup wizard should reset the user to the selected database type default.");
                        Contains(setupWizard, "Schema: defaults.Schema", "Setup wizard should reset the schema to the selected database type default.");
                        Contains(setupWizard, "Hostname", "Setup wizard should expose hostname for network databases.");
                        Contains(setupWizard, "Password", "Setup wizard should expose password for network databases.");
                        Contains(setupWizard, "Max Concurrent Requests", "Setup wizard should expose provider concurrency limit.");
                        Contains(setupWizard, "setup-provider-toggles", "Setup wizard provider checkboxes should be grouped in an aligned row.");
                        Contains(stylesheet, ".setup-provider-toggles", "Setup wizard provider checkbox row should have dedicated alignment styling.");
                        Contains(setupWizard, "allowedQueryOptions.map", "Setup wizard should render allowed queries as checkboxes.");
                        Contains(setupWizard, "AllowedQueries: [...allowedQueryOptions]", "Setup wizard should check every allowed operation by default for new database setup.");
                        Contains(setupWizard, "type=\"checkbox\"", "Allowed query operations should be checkbox inputs.");
                        Contains(setupWizard, "function updateProviderType", "Setup wizard should centralize provider type changes.");
                        Contains(setupWizard, "setProvider(createProviderDefaults(type))", "Setup wizard should replace provider defaults when provider type changes.");
                        Contains(setupWizard, "https://generativelanguage.googleapis.com", "Setup wizard should default Gemini to the Gemini API endpoint.");
                        Contains(setupWizard, "gemini-2.5-flash", "Setup wizard should default Gemini to a Gemini model.");
                        Contains(setupWizard, "gpt-4o-mini", "Setup wizard should default OpenAI to an OpenAI model.");
                        Contains(setupWizard, "buildDatabaseCandidate", "Setup wizard should sanitize database payloads before test/save.");
                        Contains(stylesheet, ".allowed-query-options", "Allowed query checkbox group should be styled.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "SetupWizardCrawlLogAutoScrolls", "Setup wizard crawl log scrolls with progress", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(setupWizard, "crawlLogRef", "Setup wizard should keep a ref to the crawl log.");
                        Contains(setupWizard, "requestAnimationFrame", "Setup wizard should scroll after the crawl log renders.");
                        Contains(setupWizard, "log.scrollTop = log.scrollHeight", "Setup wizard crawl log should scroll to the newest progress entry.");
                        Contains(setupWizard, "ref={crawlLogRef}", "Crawl log element should attach the scroll ref.");
                        Contains(stylesheet, ".setup-log", "Setup wizard crawl log should have dedicated sizing.");
                        Contains(stylesheet, "height: 180px;", "Setup wizard crawl log should keep a fixed height so the modal does not expand.");
                        Contains(stylesheet, "max-height: 180px;", "Setup wizard crawl log should scroll before the modal grows.");
                        Contains(stylesheet, "overflow-y: auto;", "Setup wizard crawl log should scroll as progress entries arrive.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "SetupWizardCanBeDismissed", "Setup wizard exposes dismissal controls", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string types = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "types", "index.ts"));

                        Contains(setupWizard, "function dismissSetup", "Setup wizard should expose a dismissal handler.");
                        Contains(setupWizard, "/v1/setup/dismiss", "Setup wizard should persist dismissal through the setup API.");
                        Contains(setupWizard, "Exit setup wizard", "Setup wizard should provide an accessible close control.");
                        Contains(setupWizard, "Skip setup", "Setup wizard should provide an explicit skip action.");
                        Contains(types, "DismissedUtc", "Setup state contract should include dismissal timestamp.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "SetupWizardProviderTestShowsModal", "Setup wizard provider validation shows a blocking spinner modal", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(setupWizard, "providerTesting", "Setup wizard should track provider validation separately from other busy states.");
                        Contains(setupWizard, "if (providerTesting) return;", "Provider validation should ignore duplicate clicks while a test is running.");
                        Contains(setupWizard, "disabled={busy || providerTesting}", "Test Provider button should be inaccessible while validation is running.");
                        Contains(setupWizard, "setup-validation-modal", "Setup wizard should render a provider validation modal.");
                        Contains(setupWizard, "setup-spinner", "Provider validation modal should include a spinner.");
                        Contains(stylesheet, ".setup-validation-backdrop", "Provider validation modal should have blocking backdrop styling.");
                        Contains(stylesheet, "@keyframes setup-spinner-rotate", "Provider validation spinner should be animated.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "SetupWizardHasConsistentVerticalSpacing", "Setup wizard keeps adequate body and action spacing", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(stylesheet, ".setup-body", "Setup wizard should have dedicated body spacing.");
                        Contains(stylesheet, "min-height: 120px;", "Short setup steps should reserve enough body height above actions.");
                        Contains(stylesheet, ".setup-wizard .modal-actions", "Setup wizard should have setup-specific action spacing.");
                        Contains(setupWizard, "This operation may take some time, please be patient.", "Database context generation step should warn users that the model operation can take time.");
                        Contains(stylesheet, "margin-top: 24px;", "Setup wizard actions should be separated from body copy.");
                        Contains(stylesheet, "padding-top: 18px;", "Setup wizard actions should have top padding.");
                        Contains(stylesheet, "border-top: 1px solid var(--border-color);", "Setup wizard actions should have a visual separation from step content.");
                        return Task.CompletedTask;
                    }),
                    Case("DashboardApiContract", "SetupWizardTableContextUsesDenseTable", "Setup wizard table context uses a dense table view", ct =>
                    {
                        string repositoryRoot = FindRepositoryRoot();
                        string setupWizard = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "components", "SetupWizard.tsx"));
                        string stylesheet = File.ReadAllText(Path.Combine(repositoryRoot, "dashboard", "src", "index.css"));

                        Contains(setupWizard, "setup-table-context-table", "Setup wizard table context step should render a table view.");
                        Contains(setupWizard, "<th>Table</th>", "Setup wizard table context step should have a table-name column.");
                        Contains(setupWizard, "<th>Context</th>", "Setup wizard table context step should have a context column.");
                        Contains(setupWizard, "rows={2}", "Setup wizard table context editors should be compact.");
                        Contains(stylesheet, "table-layout: fixed;", "Setup wizard table context table should use stable column widths.");
                        Contains(stylesheet, "width: 210px;", "Setup wizard table context name column should be constrained.");
                        Contains(stylesheet, "min-height: 58px;", "Setup wizard table context text areas should avoid oversized rows.");
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
            normalized = normalized.Replace("${database.Id}", "{id}", StringComparison.Ordinal);
            normalized = normalized.Replace("${detail?.DatabaseId}", "{id}", StringComparison.Ordinal);
            normalized = normalized.Replace("${provider.Id}", "{id}", StringComparison.Ordinal);
            normalized = normalized.Replace("${tableId}", "{tableId}", StringComparison.Ordinal);
            normalized = normalized.Replace("${table.TableId}", "{tableId}", StringComparison.Ordinal);
            return normalized;
        }

        private static List<ApiRouteContract> DashboardApiContracts()
        {
            return new List<ApiRouteContract>
            {
                new ApiRouteContract("GET", "/v1/setup", "rest.Get(\"/v1/setup\"", "apiFetch('/v1/setup')", "/v1/setup"),
                new ApiRouteContract("PUT", "/v1/setup", "rest.Put<SetupStateUpdateRequest>(\"/v1/setup\"", "apiFetch('/v1/setup'", "/v1/setup"),
                new ApiRouteContract("POST", "/v1/setup/complete", "rest.Post(\"/v1/setup/complete\"", "/v1/setup/complete", "/v1/setup/complete"),
                new ApiRouteContract("POST", "/v1/setup/dismiss", "rest.Post(\"/v1/setup/dismiss\"", "/v1/setup/dismiss", "/v1/setup/dismiss"),
                new ApiRouteContract("GET", "/v1/model", "rest.Get(\"/v1/model\"", "/v1/model?", "/v1/model"),
                new ApiRouteContract("GET", "/v1/model/{id}", "rest.Get(\"/v1/model/{id}\"", "/v1/model/${id}", "/v1/model/{id}"),
                new ApiRouteContract("POST", "/v1/model", "rest.Post<ModelProviderUpdate>(\"/v1/model\"", "apiFetch('/v1/model'", "/v1/model"),
                new ApiRouteContract("PUT", "/v1/model/{id}", "rest.Put<ModelProviderUpdate>(\"/v1/model/{id}\"", "method: 'PUT'", "/v1/model/{id}"),
                new ApiRouteContract("DELETE", "/v1/model/{id}", "rest.Delete(\"/v1/model/{id}\"", "method: 'DELETE'", "/v1/model/{id}"),
                new ApiRouteContract("POST", "/v1/model/test", "rest.Post<ProviderConnectivityTestRequest>(\"/v1/model/test\"", "/v1/model/test", "/v1/model/test"),
                new ApiRouteContract("POST", "/v1/model/{id}/test", "rest.Post(\"/v1/model/{id}/test\"", "/v1/model/${id}/test", "/v1/model/{id}/test"),
                new ApiRouteContract("GET", "/v1/database", "rest.Get(\"/v1/database\"", "/v1/database?", "/v1/database"),
                new ApiRouteContract("GET", "/v1/database/{id}", "rest.Get(\"/v1/database/{id}\"", "/v1/database/${id}", "/v1/database/{id}"),
                new ApiRouteContract("POST", "/v1/database", "rest.Post<DatabaseEntry>(\"/v1/database\"", "apiFetch('/v1/database', { method: 'POST'", "/v1/database"),
                new ApiRouteContract("PUT", "/v1/database/{id}", "rest.Put<DatabaseEntry>(\"/v1/database/{id}\"", "method: 'PUT'", "/v1/database/{id}"),
                new ApiRouteContract("DELETE", "/v1/database/{id}", "rest.Delete(\"/v1/database/{id}\"", "method: 'DELETE'", "/v1/database/{id}"),
                new ApiRouteContract("POST", "/v1/database/test", "rest.Post<DatabaseConnectivityTestRequest>(\"/v1/database/test\"", "/v1/database/test", "/v1/database/test"),
                new ApiRouteContract("POST", "/v1/database/{id}/test", "rest.Post(\"/v1/database/{id}/test\"", "/v1/database/${id}/test", "/v1/database/{id}/test"),
                new ApiRouteContract("GET", "/v1/database/{id}/table-context", "rest.Get(\"/v1/database/{id}/table-context\"", "/table-context", "/v1/database/{id}/table-context"),
                new ApiRouteContract("PUT", "/v1/database/{id}/table-context/{tableId}", "rest.Put<TableContextUpdateRequest>(\"/v1/database/{id}/table-context/{tableId}\"", "/table-context/${table.TableId}", "/v1/database/{id}/table-context/{tableId}"),
                new ApiRouteContract("POST", "/v1/database/{id}/table-context/{tableId}/build", "rest.Post<BuildTableContextRequest>(\"/v1/database/{id}/table-context/{tableId}/build\"", "/table-context/${tableId}/build", "/v1/database/{id}/table-context/{tableId}/build"),
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

            WithTempPersistenceAsync(async persistence =>
            {
                CrawlCache crawlCache = new CrawlCache();

                McpToolRegistrar.RegisterAll(
                    (name, description, inputSchema, handler) =>
                    {
                        descriptions[name] = description;
                    },
                    persistence,
                    crawlCache);
                await Task.CompletedTask.ConfigureAwait(false);
            }).GetAwaiter().GetResult();

            return descriptions;
        }

        private static Dictionary<string, Func<object, Task<object>>> RegisteredTools(
            DatabaseDriverBase persistence,
            CrawlCache crawlCache)
        {
            Dictionary<string, Func<object, Task<object>>> tools = new Dictionary<string, Func<object, Task<object>>>(StringComparer.OrdinalIgnoreCase);

            McpToolRegistrar.RegisterAll(
                (name, description, inputSchema, handler) =>
                {
                    tools[name] = handler;
                },
                persistence,
                crawlCache);

            return tools;
        }

        private static T ConvertObject<T>(object obj)
        {
            string json = Serializer.SerializeJson(obj);
            return Serializer.DeserializeJson<T>(json);
        }

        private static async Task ConfigureOnlyDatabaseAsync(DatabaseDriverBase persistence, DatabaseEntry entry, CancellationToken token)
        {
            List<DatabaseEntry> databases = await persistence.DatabaseConnections.EnumerateAsync(1000, 0, null, token).ConfigureAwait(false);
            foreach (DatabaseEntry database in databases)
            {
                await persistence.DatabaseConnections.DeleteAsync(database.Id, token).ConfigureAwait(false);
            }

            await persistence.DatabaseConnections.CreateAsync(entry, token).ConfigureAwait(false);
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

        private static async Task WithTempPersistenceAsync(Func<DatabaseDriverBase, Task> action)
        {
            string filename = GetTempDatabaseFilename();

            try
            {
                SqliteDatabaseDriver driver = new SqliteDatabaseDriver(filename);
                await driver.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                await action(driver).ConfigureAwait(false);
            }
            finally
            {
                TryDelete(filename);
                TryDelete(filename + "-wal");
                TryDelete(filename + "-shm");
            }
        }

        private static string GetTempDatabaseFilename()
        {
            return Path.Combine(Path.GetTempPath(), "tablix_persistence_" + Guid.NewGuid().ToString("N") + ".db");
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

        private static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
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
