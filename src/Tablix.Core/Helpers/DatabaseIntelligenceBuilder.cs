namespace Tablix.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using Tablix.Core.Models;
    using Tablix.Core.Settings;

    /// <summary>
    /// Builds heuristic database intelligence from crawled schema and saved context.
    /// </summary>
    public static class DatabaseIntelligenceBuilder
    {
        #region Public-Methods

        /// <summary>
        /// Build the whole-database intelligence response.
        /// </summary>
        /// <param name="database">Database entry.</param>
        /// <param name="detail">Crawled database detail.</param>
        /// <param name="includeAgentPack">Whether to include the markdown agent pack.</param>
        /// <returns>Database intelligence response.</returns>
        public static DatabaseIntelligenceResponse Build(DatabaseEntry database, DatabaseDetail detail, bool includeAgentPack = true)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            DatabaseDetail safeDetail = detail ?? new DatabaseDetail();
            List<RelationshipDetail> relationships = BuildRelationships(safeDetail, true);
            DomainIntelligence domain = BuildDomain(safeDetail, relationships);
            List<AmbiguitySignal> ambiguities = BuildGlobalAmbiguities(safeDetail, database == null ? safeDetail.Context : database.Context);
            ContextQualityScore quality = BuildContextQuality(safeDetail, relationships, ambiguities);
            AgentPackResponse agentPack = includeAgentPack ? BuildAgentPack(database, safeDetail, domain, relationships, ambiguities, quality) : null;
            stopwatch.Stop();

            return new DatabaseIntelligenceResponse
            {
                Success = true,
                DatabaseId = database == null ? safeDetail.DatabaseId : database.Id,
                Domain = domain,
                Relationships = relationships,
                Ambiguities = ambiguities,
                ContextQuality = quality,
                AgentPack = agentPack,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }

        /// <summary>
        /// Build declared and optionally inferred relationships.
        /// </summary>
        /// <param name="detail">Crawled database detail.</param>
        /// <param name="includeInferred">Whether to include inferred relationship candidates.</param>
        /// <returns>Relationship list.</returns>
        public static List<RelationshipDetail> BuildRelationships(DatabaseDetail detail, bool includeInferred)
        {
            List<RelationshipDetail> relationships = BuildDeclaredRelationships(detail);
            if (includeInferred)
            {
                HashSet<string> existing = new HashSet<string>(relationships.Select(RelationshipKey), StringComparer.OrdinalIgnoreCase);
                List<RelationshipDetail> inferred = BuildInferredRelationships(detail, existing);
                relationships.AddRange(inferred);
            }

            return relationships
                .OrderBy(relationship => relationship.FromSchema)
                .ThenBy(relationship => relationship.FromTable)
                .ThenBy(relationship => relationship.FromColumn)
                .ThenBy(relationship => relationship.Source)
                .ToList();
        }

        /// <summary>
        /// Find prompt-specific ambiguity signals.
        /// </summary>
        /// <param name="detail">Crawled database detail.</param>
        /// <param name="context">Saved database context.</param>
        /// <param name="message">Latest user message.</param>
        /// <returns>Ambiguity signals.</returns>
        public static List<AmbiguitySignal> FindPromptAmbiguities(DatabaseDetail detail, string context, string message)
        {
            return BuildAmbiguities(detail, context, message, true);
        }

        /// <summary>
        /// Build an agent pack response for a database.
        /// </summary>
        /// <param name="database">Database entry.</param>
        /// <param name="detail">Crawled database detail.</param>
        /// <param name="domain">Domain intelligence.</param>
        /// <param name="relationships">Relationship list.</param>
        /// <param name="ambiguities">Ambiguity signals.</param>
        /// <param name="quality">Context quality score.</param>
        /// <returns>Agent pack.</returns>
        public static AgentPackResponse BuildAgentPack(
            DatabaseEntry database,
            DatabaseDetail detail,
            DomainIntelligence domain,
            List<RelationshipDetail> relationships,
            List<AmbiguitySignal> ambiguities,
            ContextQualityScore quality)
        {
            string databaseId = database == null ? detail.DatabaseId : database.Id;
            string databaseName = database == null ? detail.DatabaseName : (database.Name ?? database.DatabaseName ?? database.Filename ?? database.Id);
            List<string> instructions = new List<string>
            {
                "Start with tablix_discover_databases and select databaseId " + databaseId + ".",
                "Use saved database and table context as guidance, then validate table geometry before writing SQL.",
                "Prefer tablix_list_tables and tablix_list_relationships before requesting full table geometry on large schemas.",
                "Execute only one permitted SQL statement with no semicolons and only after checking AllowedQueries.",
                "Ask a clarification question before executing SQL when an ambiguity signal applies."
            };

            List<string> suggestedQuestions = BuildSuggestedQuestions(domain, detail);
            StringBuilder markdown = new StringBuilder();
            markdown.AppendLine("# Tablix Agent Pack: " + databaseName);
            markdown.AppendLine();
            markdown.AppendLine("DatabaseId: `" + databaseId + "`");
            markdown.AppendLine("Type: `" + detail.Type + "`");
            markdown.AppendLine("Schema: `" + (detail.Schema ?? "(default)") + "`");
            if (database != null)
                markdown.AppendLine("AllowedQueries: `" + String.Join(", ", database.AllowedQueries) + "`");
            markdown.AppendLine("ContextQuality: `" + quality.Score + "/100 " + quality.Label + "`");
            markdown.AppendLine();
            markdown.AppendLine("## Domain");
            markdown.AppendLine(domain.Summary ?? "No domain summary is available.");
            markdown.AppendLine();
            markdown.AppendLine("## Main Entities");
            foreach (DomainEntity entity in domain.Entities.Take(12))
            {
                markdown.AppendLine("- `" + Qualify(entity.SchemaName, entity.TableName) + "`: " + entity.Role + "; " + entity.Summary);
            }
            markdown.AppendLine();
            markdown.AppendLine("## Relationships");
            foreach (RelationshipDetail relationship in relationships.Take(20))
            {
                markdown.AppendLine("- `" + Qualify(relationship.FromSchema, relationship.FromTable) + "." + relationship.FromColumn + "` -> `" + Qualify(relationship.ToSchema, relationship.ToTable) + "." + relationship.ToColumn + "` (" + relationship.Source + ", " + relationship.Confidence.ToString("0.00") + ")");
            }
            if (relationships.Count == 0)
                markdown.AppendLine("- No declared or inferred relationship candidates were found.");
            markdown.AppendLine();
            markdown.AppendLine("## Ambiguities To Clarify");
            foreach (AmbiguitySignal signal in ambiguities.Take(8))
            {
                markdown.AppendLine("- " + signal.Question + " Candidates: " + String.Join("; ", signal.Candidates.Take(6)) + ".");
            }
            if (ambiguities.Count == 0)
                markdown.AppendLine("- No high-signal ambiguities were detected.");
            markdown.AppendLine();
            markdown.AppendLine("## Instructions");
            foreach (string instruction in instructions)
            {
                markdown.AppendLine("- " + instruction);
            }
            markdown.AppendLine();
            markdown.AppendLine("## Starter Questions");
            foreach (string question in suggestedQuestions)
            {
                markdown.AppendLine("- " + question);
            }

            return new AgentPackResponse
            {
                Success = true,
                DatabaseId = databaseId,
                GeneratedUtc = DateTime.UtcNow,
                Markdown = markdown.ToString().Trim(),
                Instructions = instructions,
                SuggestedQuestions = suggestedQuestions
            };
        }

        #endregion

        #region Private-Methods

        private static List<RelationshipDetail> BuildDeclaredRelationships(DatabaseDetail detail)
        {
            List<RelationshipDetail> relationships = new List<RelationshipDetail>();
            if (detail == null || detail.Tables == null) return relationships;

            foreach (TableDetail table in detail.Tables)
            {
                foreach (ForeignKeyDetail foreignKey in table.ForeignKeys)
                {
                    TableDetail referencedTable = detail.Tables.FirstOrDefault(candidate =>
                        String.Equals(candidate.TableName, foreignKey.ReferencedTable, StringComparison.OrdinalIgnoreCase));

                    relationships.Add(new RelationshipDetail
                    {
                        FromSchema = table.SchemaName,
                        FromTable = table.TableName,
                        FromColumn = foreignKey.ColumnName,
                        ToSchema = referencedTable == null ? null : referencedTable.SchemaName,
                        ToTable = foreignKey.ReferencedTable,
                        ToColumn = foreignKey.ReferencedColumn,
                        ConstraintName = foreignKey.ConstraintName,
                        Source = "declared_fk",
                        Confidence = 1.0
                    });
                }
            }

            return relationships;
        }

        private static List<RelationshipDetail> BuildInferredRelationships(DatabaseDetail detail, HashSet<string> existing)
        {
            List<RelationshipDetail> relationships = new List<RelationshipDetail>();
            if (detail == null || detail.Tables == null) return relationships;

            foreach (TableDetail sourceTable in detail.Tables)
            {
                foreach (ColumnDetail sourceColumn in sourceTable.Columns)
                {
                    string entityToken = GetForeignKeyEntityToken(sourceColumn.ColumnName);
                    if (String.IsNullOrWhiteSpace(entityToken)) continue;

                    foreach (TableDetail targetTable in detail.Tables)
                    {
                        if (ReferenceEquals(sourceTable, targetTable)) continue;
                        if (!String.IsNullOrWhiteSpace(sourceTable.TableId) &&
                            !String.IsNullOrWhiteSpace(targetTable.TableId) &&
                            String.Equals(sourceTable.TableId, targetTable.TableId, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!TableMatchesEntityToken(targetTable.TableName, entityToken)) continue;

                        ColumnDetail targetColumn = GetPreferredKeyColumn(targetTable);
                        if (targetColumn == null) continue;

                        RelationshipDetail relationship = new RelationshipDetail
                        {
                            FromSchema = sourceTable.SchemaName,
                            FromTable = sourceTable.TableName,
                            FromColumn = sourceColumn.ColumnName,
                            ToSchema = targetTable.SchemaName,
                            ToTable = targetTable.TableName,
                            ToColumn = targetColumn.ColumnName,
                            ConstraintName = null,
                            Source = "inferred_name_match",
                            Confidence = SourceTypeConfidence(sourceColumn, targetColumn, targetTable.TableName, entityToken)
                        };

                        string key = RelationshipKey(relationship);
                        if (existing.Contains(key)) continue;

                        existing.Add(key);
                        relationships.Add(relationship);
                    }
                }
            }

            return relationships;
        }

        private static DomainIntelligence BuildDomain(DatabaseDetail detail, List<RelationshipDetail> relationships)
        {
            DomainIntelligence domain = new DomainIntelligence();
            int tableCount = detail == null || detail.Tables == null ? 0 : detail.Tables.Count;
            int declaredCount = relationships.Count(relationship => String.Equals(relationship.Source, "declared_fk", StringComparison.OrdinalIgnoreCase));
            int inferredCount = relationships.Count - declaredCount;

            domain.Summary = "Crawled " + tableCount + " table(s), " + declaredCount + " declared relationship(s), and " + inferredCount + " inferred relationship candidate(s).";
            if (!String.IsNullOrWhiteSpace(detail.Context))
                domain.Summary = domain.Summary + " Saved database context is available.";

            if (detail == null || detail.Tables == null) return domain;

            foreach (TableDetail table in detail.Tables.OrderBy(table => table.SchemaName).ThenBy(table => table.TableName))
            {
                DomainEntity entity = new DomainEntity
                {
                    TableId = table.TableId,
                    SchemaName = table.SchemaName,
                    TableName = table.TableName,
                    Role = InferTableRole(table, relationships),
                    Summary = BuildEntitySummary(table),
                    KeyColumns = SelectKeyColumns(table),
                    HasContext = !String.IsNullOrWhiteSpace(table.Context)
                };
                domain.Entities.Add(entity);
            }

            foreach (RelationshipDetail relationship in relationships.Take(30))
            {
                AddDistinct(domain.Workflows, Qualify(relationship.FromSchema, relationship.FromTable) + " joins to " + Qualify(relationship.ToSchema, relationship.ToTable) + " through " + relationship.FromColumn + " -> " + relationship.ToColumn);
            }

            foreach (TableDetail table in detail.Tables)
            {
                foreach (ColumnDetail column in table.Columns)
                {
                    string label = Qualify(table.SchemaName, table.TableName) + "." + column.ColumnName;
                    if (IsMetricColumn(column.ColumnName, column.DataType)) AddDistinct(domain.Metrics, label);
                    if (IsCommonFilterColumn(column.ColumnName)) AddDistinct(domain.CommonFilters, label);
                    if (IsFreshnessColumn(column.ColumnName, column.DataType)) AddDistinct(domain.FreshnessColumns, label);
                    if (IsTenantColumn(column.ColumnName)) AddDistinct(domain.TenantColumns, label);
                    if (IsSoftDeleteColumn(column.ColumnName)) AddDistinct(domain.SoftDeleteColumns, label);
                }
            }

            domain.Metrics = domain.Metrics.Take(40).ToList();
            domain.CommonFilters = domain.CommonFilters.Take(40).ToList();
            domain.FreshnessColumns = domain.FreshnessColumns.Take(40).ToList();
            domain.TenantColumns = domain.TenantColumns.Take(40).ToList();
            domain.SoftDeleteColumns = domain.SoftDeleteColumns.Take(40).ToList();
            return domain;
        }

        private static List<AmbiguitySignal> BuildGlobalAmbiguities(DatabaseDetail detail, string context)
        {
            return BuildAmbiguities(detail, context, null, false);
        }

        private static List<AmbiguitySignal> BuildAmbiguities(DatabaseDetail detail, string context, string message, bool promptSpecific)
        {
            List<AmbiguitySignal> signals = new List<AmbiguitySignal>();
            if (detail == null || detail.Tables == null) return signals;

            string normalizedMessage = message == null ? null : message.ToLowerInvariant();
            List<string> freshness = FindColumnLabels(detail, (column) => IsFreshnessColumn(column.ColumnName, column.DataType));
            List<string> activity = FindColumnLabels(detail, (column) => IsActivityColumn(column.ColumnName) || IsSoftDeleteColumn(column.ColumnName));
            List<string> statuses = FindColumnLabels(detail, (column) => IsStatusColumn(column.ColumnName));
            List<string> metrics = FindColumnLabels(detail, (column) => IsMetricColumn(column.ColumnName, column.DataType));
            List<string> ownerColumns = FindColumnLabels(detail, (column) => ContainsAny(column.ColumnName, "owner", "assignee", "created_by", "updated_by"));
            List<string> partyTables = FindTableLabels(detail, (table) => ContainsAny(table.TableName, "customer", "client", "user", "account"));

            AddAmbiguityIfNeeded(signals, promptSpecific, normalizedMessage, "latest", new List<string> { "latest", "recent", "newest" }, freshness, "Which timestamp defines latest?", "Multiple timestamp columns could define latest records.");
            AddAmbiguityIfNeeded(signals, promptSpecific, normalizedMessage, "active", new List<string> { "active", "inactive", "enabled" }, activity, "Which column or rule defines active records?", "Multiple activity, status, or soft-delete columns could define active records.");
            AddAmbiguityIfNeeded(signals, promptSpecific, normalizedMessage, "status", new List<string> { "status", "state" }, statuses, "Which status column should be used?", "Multiple status-like columns exist.");
            AddAmbiguityIfNeeded(signals, promptSpecific, normalizedMessage, "revenue", new List<string> { "revenue", "sales", "amount", "total" }, metrics, "Which metric column defines revenue or total value?", "Multiple numeric amount-like columns exist.");
            AddAmbiguityIfNeeded(signals, promptSpecific, normalizedMessage, "owner", new List<string> { "owner", "assignee" }, ownerColumns, "Which ownership column should be used?", "Multiple owner-like columns exist.");
            AddAmbiguityIfNeeded(signals, promptSpecific, normalizedMessage, "customer", new List<string> { "customer", "client", "user", "account" }, partyTables, "Which party table should represent the user or customer?", "Multiple person, customer, user, or account tables exist.");

            if (!promptSpecific && String.IsNullOrWhiteSpace(context) && detail.Tables.Count > 0)
            {
                signals.Add(new AmbiguitySignal
                {
                    Term = "business_context",
                    Reason = "No database-level context is saved.",
                    Question = "What business definitions should agents use for this database?",
                    Candidates = new List<string> { "Generate database context", "Edit database context", "Add table-level context for core tables" }
                });
            }

            return signals.Take(12).ToList();
        }

        private static ContextQualityScore BuildContextQuality(DatabaseDetail detail, List<RelationshipDetail> relationships, List<AmbiguitySignal> ambiguities)
        {
            ContextQualityScore quality = new ContextQualityScore();
            int tableCount = detail == null || detail.Tables == null ? 0 : detail.Tables.Count;
            int tablesWithContext = detail == null || detail.Tables == null ? 0 : detail.Tables.Count(table => !String.IsNullOrWhiteSpace(table.Context));
            int declaredRelationships = relationships.Count(relationship => String.Equals(relationship.Source, "declared_fk", StringComparison.OrdinalIgnoreCase));
            int inferredRelationships = relationships.Count - declaredRelationships;

            quality.TotalTables = tableCount;
            quality.TablesWithContext = tablesWithContext;
            quality.DeclaredRelationships = declaredRelationships;
            quality.InferredRelationships = inferredRelationships;

            int score = 0;
            if (detail != null && detail.IsCrawled) score += 20;
            else AddSignal(quality, "crawl", "blocker", "Schema is not successfully crawled.", "Run a crawl before using this database with agents.");

            if (detail != null && !String.IsNullOrWhiteSpace(detail.Context)) score += 20;
            else AddSignal(quality, "database_context", "warning", "Database-level context is missing.", "Build or write database context that defines purpose, major entities, and caveats.");

            if (tableCount > 0)
            {
                int coverageScore = (int)Math.Round((double)tablesWithContext / tableCount * 30.0);
                score += coverageScore;
                if (coverageScore < 20)
                    AddSignal(quality, "table_context", "warning", "Only " + tablesWithContext + " of " + tableCount + " table(s) have saved context.", "Build table context for core tables first.");
            }

            score += Math.Min(20, declaredRelationships * 4 + inferredRelationships * 2);
            if (declaredRelationships == 0 && inferredRelationships > 0)
                AddSignal(quality, "relationships", "warning", "Only inferred relationship candidates are available.", "Review inferred relationships and save durable join guidance in context.");
            if (relationships.Count == 0 && tableCount > 1)
                AddSignal(quality, "relationships", "warning", "No relationship evidence was found.", "Add relationship guidance to database or table context.");

            if (ambiguities.Count > 0)
            {
                score -= Math.Min(20, ambiguities.Count * 4);
                AddSignal(quality, "ambiguity", "warning", ambiguities.Count + " ambiguity signal(s) may require clarification.", "Clarify definitions in database or table context.");
            }

            quality.Score = Math.Clamp(score, 0, 100);
            if (quality.Score >= 85) quality.Label = "Strong";
            else if (quality.Score >= 65) quality.Label = "Good";
            else if (quality.Score >= 40) quality.Label = "Needs context";
            else quality.Label = "Degraded";

            return quality;
        }

        private static List<string> BuildSuggestedQuestions(DomainIntelligence domain, DatabaseDetail detail)
        {
            List<string> questions = new List<string>();
            if (domain == null) return questions;

            foreach (DomainEntity entity in domain.Entities.Take(4))
            {
                questions.Add("How many records are in " + Qualify(entity.SchemaName, entity.TableName) + "?");
            }

            foreach (string metric in domain.Metrics.Take(3))
            {
                questions.Add("What is the total " + metric + " by month?");
            }

            foreach (string freshness in domain.FreshnessColumns.Take(3))
            {
                questions.Add("What are the latest records by " + freshness + "?");
            }

            return questions.Take(8).ToList();
        }

        private static string InferTableRole(TableDetail table, List<RelationshipDetail> relationships)
        {
            int outbound = relationships.Count(relationship => String.Equals(relationship.FromTable, table.TableName, StringComparison.OrdinalIgnoreCase));
            int inbound = relationships.Count(relationship => String.Equals(relationship.ToTable, table.TableName, StringComparison.OrdinalIgnoreCase));

            if (outbound >= 2 && inbound == 0 && table.Columns.Count <= outbound + 4)
                return "junction";

            if (ContainsAny(table.TableName, "log", "event", "history", "audit", "transaction"))
                return "event";

            if (ContainsAny(table.TableName, "type", "status", "category", "lookup"))
                return "lookup";

            if (inbound > 0 && outbound == 0)
                return "core entity";

            return "entity";
        }

        private static string BuildEntitySummary(TableDetail table)
        {
            List<string> parts = new List<string>();
            parts.Add(table.Columns.Count + " column(s)");
            if (table.ForeignKeys.Count > 0) parts.Add(table.ForeignKeys.Count + " declared FK(s)");
            if (!String.IsNullOrWhiteSpace(table.Context)) parts.Add("saved context");
            return "Contains " + String.Join(", ", parts) + ".";
        }

        private static List<string> SelectKeyColumns(TableDetail table)
        {
            List<string> columns = new List<string>();
            foreach (ColumnDetail column in table.Columns)
            {
                if (column.IsPrimaryKey || IsLikelyForeignKey(column.ColumnName) || IsCommonFilterColumn(column.ColumnName) || IsMetricColumn(column.ColumnName, column.DataType))
                    AddDistinct(columns, column.ColumnName);
            }

            if (columns.Count == 0)
            {
                foreach (ColumnDetail column in table.Columns.Take(6))
                {
                    AddDistinct(columns, column.ColumnName);
                }
            }

            return columns.Take(10).ToList();
        }

        private static ColumnDetail GetPreferredKeyColumn(TableDetail table)
        {
            ColumnDetail primary = table.Columns.FirstOrDefault(column => column.IsPrimaryKey);
            if (primary != null) return primary;

            ColumnDetail id = table.Columns.FirstOrDefault(column => String.Equals(column.ColumnName, "id", StringComparison.OrdinalIgnoreCase));
            if (id != null) return id;

            string singular = Singularize(NormalizeIdentifier(table.TableName));
            return table.Columns.FirstOrDefault(column => NormalizeIdentifier(column.ColumnName) == singular + "id");
        }

        private static double SourceTypeConfidence(ColumnDetail sourceColumn, ColumnDetail targetColumn, string targetTableName, string entityToken)
        {
            double confidence = 0.74;
            string source = NormalizeIdentifier(sourceColumn.ColumnName);
            string target = Singularize(NormalizeIdentifier(targetTableName));
            if (source == target + "id") confidence = 0.86;
            if (String.Equals(targetColumn.ColumnName, "id", StringComparison.OrdinalIgnoreCase)) confidence += 0.04;
            return Math.Min(confidence, 0.95);
        }

        private static bool TableMatchesEntityToken(string tableName, string entityToken)
        {
            string normalizedTable = NormalizeIdentifier(tableName);
            string singularTable = Singularize(normalizedTable);
            string normalizedToken = NormalizeIdentifier(entityToken);
            string singularToken = Singularize(normalizedToken);
            return normalizedToken == normalizedTable ||
                   normalizedToken == singularTable ||
                   singularToken == singularTable ||
                   normalizedTable == singularToken + "s";
        }

        private static string GetForeignKeyEntityToken(string columnName)
        {
            if (!IsLikelyForeignKey(columnName)) return null;

            string normalized = NormalizeIdentifier(columnName);
            if (normalized == "id") return null;
            if (normalized.EndsWith("id", StringComparison.OrdinalIgnoreCase))
                return normalized.Substring(0, normalized.Length - 2);

            return null;
        }

        private static bool IsLikelyForeignKey(string columnName)
        {
            string normalized = NormalizeIdentifier(columnName);
            return normalized.Length > 2 && normalized.EndsWith("id", StringComparison.OrdinalIgnoreCase) && normalized != "id";
        }

        private static bool IsMetricColumn(string columnName, string dataType)
        {
            if (!ContainsAny(columnName, "amount", "total", "price", "cost", "revenue", "quantity", "qty", "balance")) return false;
            if (String.IsNullOrWhiteSpace(dataType)) return true;
            return ContainsAny(dataType, "int", "decimal", "numeric", "number", "real", "double", "float", "money");
        }

        private static bool IsCommonFilterColumn(string columnName)
        {
            return IsStatusColumn(columnName) || IsActivityColumn(columnName) || IsTenantColumn(columnName) || IsSoftDeleteColumn(columnName) || IsFreshnessColumn(columnName, null);
        }

        private static bool IsStatusColumn(string columnName)
        {
            return ContainsAny(columnName, "status", "state", "stage");
        }

        private static bool IsActivityColumn(string columnName)
        {
            return ContainsAny(columnName, "active", "enabled", "disabled");
        }

        private static bool IsTenantColumn(string columnName)
        {
            return ContainsAny(columnName, "tenant_id", "account_id", "org_id", "organization_id", "company_id");
        }

        private static bool IsSoftDeleteColumn(string columnName)
        {
            return ContainsAny(columnName, "deleted_at", "deleted_on", "is_deleted", "archived_at", "is_archived");
        }

        private static bool IsFreshnessColumn(string columnName, string dataType)
        {
            bool nameMatches = ContainsAny(columnName, "created_at", "created_on", "updated_at", "updated_on", "date", "time", "timestamp");
            if (!nameMatches) return false;
            if (String.IsNullOrWhiteSpace(dataType)) return true;
            return ContainsAny(dataType, "date", "time", "timestamp", "datetime");
        }

        private static List<string> FindColumnLabels(DatabaseDetail detail, Func<ColumnDetail, bool> predicate)
        {
            List<string> labels = new List<string>();
            foreach (TableDetail table in detail.Tables)
            {
                foreach (ColumnDetail column in table.Columns)
                {
                    if (predicate(column))
                        AddDistinct(labels, Qualify(table.SchemaName, table.TableName) + "." + column.ColumnName);
                }
            }

            return labels;
        }

        private static List<string> FindTableLabels(DatabaseDetail detail, Func<TableDetail, bool> predicate)
        {
            List<string> labels = new List<string>();
            foreach (TableDetail table in detail.Tables)
            {
                if (predicate(table))
                    AddDistinct(labels, Qualify(table.SchemaName, table.TableName));
            }

            return labels;
        }

        private static void AddAmbiguityIfNeeded(
            List<AmbiguitySignal> signals,
            bool promptSpecific,
            string normalizedMessage,
            string term,
            List<string> triggers,
            List<string> candidates,
            string question,
            string reason)
        {
            if (candidates.Count <= 1) return;
            if (promptSpecific)
            {
                if (String.IsNullOrWhiteSpace(normalizedMessage)) return;
                bool mentioned = triggers.Any(trigger => normalizedMessage.Contains(trigger, StringComparison.OrdinalIgnoreCase));
                bool explicitCandidate = candidates.Any(candidate => normalizedMessage.Contains(candidate.Split('.').Last(), StringComparison.OrdinalIgnoreCase));
                if (!mentioned || explicitCandidate) return;
            }

            signals.Add(new AmbiguitySignal
            {
                Term = term,
                Reason = reason,
                Question = question,
                Candidates = candidates.Take(10).ToList()
            });
        }

        private static void AddSignal(ContextQualityScore quality, string key, string severity, string message, string recommendation)
        {
            quality.Signals.Add(new ContextQualitySignal
            {
                Key = key,
                Severity = severity,
                Message = message,
                Recommendation = recommendation
            });
        }

        private static string RelationshipKey(RelationshipDetail relationship)
        {
            return NormalizeIdentifier(relationship.FromSchema) + "." +
                   NormalizeIdentifier(relationship.FromTable) + "." +
                   NormalizeIdentifier(relationship.FromColumn) + "->" +
                   NormalizeIdentifier(relationship.ToSchema) + "." +
                   NormalizeIdentifier(relationship.ToTable) + "." +
                   NormalizeIdentifier(relationship.ToColumn);
        }

        private static string Qualify(string schemaName, string tableName)
        {
            if (String.IsNullOrWhiteSpace(schemaName)) return tableName ?? String.Empty;
            return schemaName + "." + tableName;
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return;
            if (!values.Any(existing => String.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
                values.Add(value);
        }

        private static bool ContainsAny(string value, params string[] needles)
        {
            if (String.IsNullOrWhiteSpace(value)) return false;
            string normalizedValue = NormalizeIdentifier(value);
            foreach (string needle in needles)
            {
                if (String.IsNullOrWhiteSpace(needle)) continue;
                if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
                    return true;

                string normalizedNeedle = NormalizeIdentifier(needle);
                if (!String.IsNullOrWhiteSpace(normalizedNeedle) &&
                    normalizedValue.Contains(normalizedNeedle, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string NormalizeIdentifier(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return String.Empty;

            StringBuilder builder = new StringBuilder();
            foreach (char character in value)
            {
                if (Char.IsLetterOrDigit(character))
                    builder.Append(Char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }

        private static string Singularize(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return value;
            if (value.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
                return value.Substring(0, value.Length - 3) + "y";
            if (value.EndsWith("ses", StringComparison.OrdinalIgnoreCase) && value.Length > 3)
                return value.Substring(0, value.Length - 2);
            if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) && value.Length > 1)
                return value.Substring(0, value.Length - 1);
            return value;
        }

        #endregion
    }
}
