namespace Tablix.Core.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Validates SQL queries against allowed statement types.
    /// </summary>
    public static class QueryValidator
    {
        #region Public-Methods

        /// <summary>
        /// Validate a SQL query against the allowed query types for a database.
        /// Returns null if valid, or an error message if invalid.
        /// </summary>
        /// <param name="query">SQL query string.</param>
        /// <param name="allowedQueries">List of allowed SQL statement types (e.g. SELECT, INSERT).</param>
        /// <returns>Null if valid, error message if invalid.</returns>
        public static string Validate(string query, List<string> allowedQueries)
        {
            if (String.IsNullOrWhiteSpace(query))
                return "Query cannot be empty.";

            if (allowedQueries == null || allowedQueries.Count == 0)
                return "No query types are permitted for this database.";

            // Reject multi-statement input
            if (query.Contains(';'))
                return "Multi-statement queries are not supported. Remove semicolons from your query.";

            // Strip leading whitespace and SQL comments
            string stripped = StripLeadingComments(query).TrimStart();

            if (String.IsNullOrWhiteSpace(stripped))
                return "Query is empty after removing comments.";

            // Extract the leading keyword
            string firstWord = stripped.Split(new[] { ' ', '\t', '\r', '\n' }, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            if (String.IsNullOrEmpty(firstWord))
                return "Unable to determine query type.";

            string normalizedKeyword = firstWord.ToUpperInvariant();

            // Check against allowed list
            bool isAllowed = allowedQueries.Any(a =>
                String.Equals(a, normalizedKeyword, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
                return "Query type '" + normalizedKeyword + "' is not permitted. Allowed types: " + String.Join(", ", allowedQueries) + ".";

            return null;
        }

        #endregion

        #region Private-Methods

        private static string StripLeadingComments(string sql)
        {
            string result = sql;

            while (true)
            {
                result = result.TrimStart();

                // Strip single-line comments
                if (result.StartsWith("--"))
                {
                    int newlineIndex = result.IndexOf('\n');
                    if (newlineIndex < 0)
                        return "";
                    result = result.Substring(newlineIndex + 1);
                    continue;
                }

                // Strip block comments
                if (result.StartsWith("/*"))
                {
                    int endIndex = result.IndexOf("*/");
                    if (endIndex < 0)
                        return "";
                    result = result.Substring(endIndex + 2);
                    continue;
                }

                break;
            }

            return result;
        }

        #endregion
    }
}
