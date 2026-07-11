namespace Tablix.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Tablix.Core.Models;

    /// <summary>
    /// Projects crawled schema geometry into compact paginated model-facing responses.
    /// </summary>
    public static class SchemaProjection
    {
        #region Public-Methods

        /// <summary>
        /// Create a paginated table summary response from a database detail.
        /// </summary>
        /// <param name="databaseId">Database entry identifier.</param>
        /// <param name="detail">Crawled database detail.</param>
        /// <param name="maxResults">Maximum results to return. Values are clamped from 1 to 1000.</param>
        /// <param name="skip">Records to skip. Negative values are treated as 0.</param>
        /// <param name="filter">Optional filter for table or schema names.</param>
        /// <param name="schema">Optional schema filter.</param>
        /// <returns>Paginated table summary response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when detail is null.</exception>
        public static DatabaseTableListResult CreateTableListResult(
            string databaseId,
            DatabaseDetail detail,
            int maxResults,
            int skip,
            string filter = null,
            string schema = null)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));

            Stopwatch stopwatch = Stopwatch.StartNew();
            int normalizedMaxResults = Math.Clamp(maxResults, 1, 1000);
            int normalizedSkip = Math.Max(skip, 0);

            List<TableSummary> tables = detail.Tables
                .Select(t => new TableSummary
                {
                    TableId = t.TableId,
                    SchemaName = t.SchemaName,
                    TableName = t.TableName,
                    Columns = t.Columns.Count,
                    ForeignKeys = t.ForeignKeys.Count,
                    Indexes = t.Indexes.Count
                })
                .OrderBy(t => t.SchemaName)
                .ThenBy(t => t.TableName)
                .ToList();

            if (!String.IsNullOrEmpty(schema))
            {
                tables = tables.Where(t =>
                    String.Equals(t.SchemaName, schema, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!String.IsNullOrEmpty(filter))
            {
                tables = tables.Where(t =>
                    Contains(t.SchemaName, filter) ||
                    Contains(t.TableName, filter)).ToList();
            }

            long totalRecords = tables.Count;
            List<TableSummary> page = tables.Skip(normalizedSkip).Take(normalizedMaxResults).ToList();
            long remaining = Math.Max(0, totalRecords - normalizedSkip - page.Count);

            stopwatch.Stop();

            return new DatabaseTableListResult
            {
                Success = true,
                DatabaseId = databaseId,
                Context = detail.Context,
                IsCrawled = detail.IsCrawled,
                TableCount = detail.Tables.Count,
                Filter = filter,
                Schema = schema,
                MaxResults = normalizedMaxResults,
                Skip = normalizedSkip,
                TotalRecords = totalRecords,
                RecordsRemaining = remaining,
                EndOfResults = remaining == 0,
                NextSkip = remaining == 0 ? null : normalizedSkip + page.Count,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                Objects = page
            };
        }

        /// <summary>
        /// Create a paginated relationship response from a database detail.
        /// </summary>
        /// <param name="databaseId">Database entry identifier.</param>
        /// <param name="detail">Crawled database detail.</param>
        /// <param name="maxResults">Maximum results to return. Values are clamped from 1 to 1000.</param>
        /// <param name="skip">Records to skip. Negative values are treated as 0.</param>
        /// <param name="filter">Optional filter for table, column, or constraint names.</param>
        /// <param name="schema">Optional schema filter.</param>
        /// <param name="includeInferred">Whether inferred relationships were requested. Only declared foreign keys are currently returned.</param>
        /// <returns>Paginated relationship response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when detail is null.</exception>
        public static DatabaseRelationshipListResult CreateRelationshipListResult(
            string databaseId,
            DatabaseDetail detail,
            int maxResults,
            int skip,
            string filter = null,
            string schema = null,
            bool includeInferred = false)
        {
            if (detail == null) throw new ArgumentNullException(nameof(detail));

            Stopwatch stopwatch = Stopwatch.StartNew();
            int normalizedMaxResults = Math.Clamp(maxResults, 1, 1000);
            int normalizedSkip = Math.Max(skip, 0);

            List<RelationshipDetail> relationships = new List<RelationshipDetail>();

            foreach (TableDetail table in detail.Tables)
            {
                foreach (ForeignKeyDetail foreignKey in table.ForeignKeys)
                {
                    TableDetail referencedTable = detail.Tables.FirstOrDefault(t =>
                        String.Equals(t.TableName, foreignKey.ReferencedTable, StringComparison.OrdinalIgnoreCase));

                    relationships.Add(new RelationshipDetail
                    {
                        FromSchema = table.SchemaName,
                        FromTable = table.TableName,
                        FromColumn = foreignKey.ColumnName,
                        ToSchema = referencedTable != null ? referencedTable.SchemaName : null,
                        ToTable = foreignKey.ReferencedTable,
                        ToColumn = foreignKey.ReferencedColumn,
                        ConstraintName = foreignKey.ConstraintName,
                        Source = "declared_fk",
                        Confidence = 1.0
                    });
                }
            }

            relationships = relationships
                .OrderBy(r => r.FromSchema)
                .ThenBy(r => r.FromTable)
                .ThenBy(r => r.FromColumn)
                .ToList();

            if (!String.IsNullOrEmpty(schema))
            {
                relationships = relationships.Where(r =>
                    String.Equals(r.FromSchema, schema, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(r.ToSchema, schema, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!String.IsNullOrEmpty(filter))
            {
                relationships = relationships.Where(r =>
                    Contains(r.FromSchema, filter) ||
                    Contains(r.FromTable, filter) ||
                    Contains(r.FromColumn, filter) ||
                    Contains(r.ToSchema, filter) ||
                    Contains(r.ToTable, filter) ||
                    Contains(r.ToColumn, filter) ||
                    Contains(r.ConstraintName, filter)).ToList();
            }

            long totalRecords = relationships.Count;
            List<RelationshipDetail> page = relationships.Skip(normalizedSkip).Take(normalizedMaxResults).ToList();
            long remaining = Math.Max(0, totalRecords - normalizedSkip - page.Count);

            stopwatch.Stop();

            return new DatabaseRelationshipListResult
            {
                Success = true,
                DatabaseId = databaseId,
                Context = detail.Context,
                IsCrawled = detail.IsCrawled,
                TableCount = detail.Tables.Count,
                Filter = filter,
                Schema = schema,
                IncludeInferred = includeInferred,
                MaxResults = normalizedMaxResults,
                Skip = normalizedSkip,
                TotalRecords = totalRecords,
                RecordsRemaining = remaining,
                EndOfResults = remaining == 0,
                NextSkip = remaining == 0 ? null : normalizedSkip + page.Count,
                TotalMs = stopwatch.Elapsed.TotalMilliseconds,
                Objects = page
            };
        }

        #endregion

        #region Private-Methods

        private static bool Contains(string value, string filter)
        {
            return value != null &&
                   filter != null &&
                   value.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
