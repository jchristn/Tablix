namespace Tablix.Core.Helpers
{
    using System;
    using System.Linq;
    using Tablix.Core.Models;

    /// <summary>
    /// Creates the stable table metadata identifiers shared by persistence, the crawl cache, and MCP tools.
    /// </summary>
    public static class TableIdentity
    {
        #region Public-Methods

        /// <summary>
        /// Create the table metadata identifier for a table.
        /// </summary>
        /// <param name="databaseId">Database entry identifier.</param>
        /// <param name="schemaName">Schema name.</param>
        /// <param name="tableName">Table name.</param>
        /// <returns>Table metadata identifier.</returns>
        public static string Create(string databaseId, string schemaName, string tableName)
        {
            string raw = (databaseId ?? String.Empty) + "_" + (schemaName ?? String.Empty) + "_" + (tableName ?? String.Empty);
            char[] chars = raw.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray();
            return "tbl_" + new string(chars).Trim('_');
        }

        /// <summary>
        /// Assign identifiers to every table in a database detail that does not already have one.
        /// </summary>
        /// <param name="detail">Database detail.</param>
        public static void Assign(DatabaseDetail detail)
        {
            if (detail == null || detail.Tables == null) return;

            foreach (TableDetail table in detail.Tables)
            {
                if (String.IsNullOrEmpty(table.TableId))
                    table.TableId = Create(detail.DatabaseId, table.SchemaName, table.TableName);
            }
        }

        #endregion
    }
}
