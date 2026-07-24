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

            query = NormalizeSingleStatement(query);

            // Reject multi-statement input
            if (query.Contains(';'))
                return "Multi-statement queries are not supported. Remove semicolons from your query.";

            // Strip leading whitespace and SQL comments
            string stripped = StripLeadingComments(query).TrimStart();

            if (String.IsNullOrWhiteSpace(stripped))
                return "Query is empty after removing comments.";

            // Extract the leading keyword
            string firstWord = GetEffectiveStatementKeyword(stripped);

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

        /// <summary>
        /// Normalize a single SQL statement for validation and execution.
        /// </summary>
        /// <param name="query">SQL query string.</param>
        /// <returns>Normalized SQL query.</returns>
        public static string NormalizeSingleStatement(string query)
        {
            if (String.IsNullOrWhiteSpace(query)) return query;

            string normalized = query.Trim();
            if (!normalized.EndsWith(";", StringComparison.Ordinal))
                return normalized;

            string withoutTerminator = normalized.Substring(0, normalized.Length - 1).TrimEnd();
            return withoutTerminator.Contains(';') ? normalized : withoutTerminator;
        }

        #endregion

        #region Private-Methods

        private static string GetEffectiveStatementKeyword(string sql)
        {
            string firstWord = ReadWord(sql, 0, out int nextIndex);
            if (!String.Equals(firstWord, "WITH", StringComparison.OrdinalIgnoreCase))
                return firstWord;

            int index = nextIndex;
            SkipWhitespace(sql, ref index);

            string maybeRecursive = ReadWord(sql, index, out int afterMaybeRecursive);
            if (String.Equals(maybeRecursive, "RECURSIVE", StringComparison.OrdinalIgnoreCase))
                index = afterMaybeRecursive;

            while (index < sql.Length)
            {
                SkipWhitespace(sql, ref index);
                string cteName = ReadWord(sql, index, out index);
                if (String.IsNullOrEmpty(cteName)) return firstWord;

                SkipWhitespace(sql, ref index);
                if (index < sql.Length && sql[index] == '(')
                {
                    index = FindMatchingParen(sql, index);
                    if (index < 0) return firstWord;
                    index++;
                }

                SkipWhitespace(sql, ref index);
                string asKeyword = ReadWord(sql, index, out index);
                if (!String.Equals(asKeyword, "AS", StringComparison.OrdinalIgnoreCase))
                    return firstWord;

                SkipWhitespace(sql, ref index);
                if (index >= sql.Length || sql[index] != '(')
                    return firstWord;

                index = FindMatchingParen(sql, index);
                if (index < 0) return firstWord;
                index++;

                SkipWhitespace(sql, ref index);
                if (index < sql.Length && sql[index] == ',')
                {
                    index++;
                    continue;
                }

                return ReadWord(sql, index, out _);
            }

            return firstWord;
        }

        private static string ReadWord(string sql, int startIndex, out int nextIndex)
        {
            nextIndex = startIndex;
            if (String.IsNullOrEmpty(sql)) return null;

            SkipWhitespace(sql, ref nextIndex);
            int wordStart = nextIndex;
            while (nextIndex < sql.Length && (Char.IsLetterOrDigit(sql[nextIndex]) || sql[nextIndex] == '_'))
            {
                nextIndex++;
            }

            return nextIndex == wordStart ? null : sql.Substring(wordStart, nextIndex - wordStart);
        }

        private static void SkipWhitespace(string sql, ref int index)
        {
            while (index < sql.Length && Char.IsWhiteSpace(sql[index]))
            {
                index++;
            }
        }

        private static int FindMatchingParen(string sql, int openParenIndex)
        {
            int depth = 0;
            for (int i = openParenIndex; i < sql.Length; i++)
            {
                char ch = sql[i];
                if (ch == '\'')
                {
                    i = SkipSingleQuotedString(sql, i);
                    continue;
                }

                if (ch == '"')
                {
                    i = SkipDoubleQuotedString(sql, i);
                    continue;
                }

                if (ch == '[')
                {
                    i = SkipBracketQuotedIdentifier(sql, i);
                    continue;
                }

                if (ch == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                {
                    i = SkipLineComment(sql, i);
                    continue;
                }

                if (ch == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
                {
                    i = SkipBlockComment(sql, i);
                    continue;
                }

                if (ch == '(')
                {
                    depth++;
                    continue;
                }

                if (ch == ')')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return -1;
        }

        private static int SkipSingleQuotedString(string sql, int quoteIndex)
        {
            for (int i = quoteIndex + 1; i < sql.Length; i++)
            {
                if (sql[i] != '\'') continue;
                if (i + 1 < sql.Length && sql[i + 1] == '\'')
                {
                    i++;
                    continue;
                }

                return i;
            }

            return sql.Length - 1;
        }

        private static int SkipDoubleQuotedString(string sql, int quoteIndex)
        {
            for (int i = quoteIndex + 1; i < sql.Length; i++)
            {
                if (sql[i] != '"') continue;
                if (i + 1 < sql.Length && sql[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                return i;
            }

            return sql.Length - 1;
        }

        private static int SkipBracketQuotedIdentifier(string sql, int bracketIndex)
        {
            int closeIndex = sql.IndexOf(']', bracketIndex + 1);
            return closeIndex < 0 ? sql.Length - 1 : closeIndex;
        }

        private static int SkipLineComment(string sql, int commentIndex)
        {
            int newlineIndex = sql.IndexOf('\n', commentIndex + 2);
            return newlineIndex < 0 ? sql.Length - 1 : newlineIndex;
        }

        private static int SkipBlockComment(string sql, int commentIndex)
        {
            int endIndex = sql.IndexOf("*/", commentIndex + 2, StringComparison.Ordinal);
            return endIndex < 0 ? sql.Length - 1 : endIndex + 1;
        }

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
