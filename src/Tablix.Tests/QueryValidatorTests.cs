namespace Tablix.Tests
{
    using System.Collections.Generic;
    using Tablix.Core.Helpers;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="QueryValidator"/> edge cases.
    /// </summary>
    public class QueryValidatorTests
    {
        /// <summary>
        /// Empty query returns an error message.
        /// </summary>
        [Fact]
        public void Validate_EmptyQuery_ReturnsError()
        {
            string result = QueryValidator.Validate("", new List<string> { "SELECT" });
            Assert.NotNull(result);
        }

        /// <summary>
        /// Null query returns an error message.
        /// </summary>
        [Fact]
        public void Validate_NullQuery_ReturnsError()
        {
            string result = QueryValidator.Validate(null, new List<string> { "SELECT" });
            Assert.NotNull(result);
        }

        /// <summary>
        /// Query containing a semicolon is rejected.
        /// </summary>
        [Fact]
        public void Validate_SemicolonInQuery_Rejected()
        {
            string result = QueryValidator.Validate("SELECT 1; SELECT 2", new List<string> { "SELECT" });
            Assert.NotNull(result);
            Assert.Contains("semicolon", result, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// SELECT query is allowed when AllowedQueries contains SELECT.
        /// </summary>
        [Fact]
        public void Validate_SelectAllowed_ReturnsNull()
        {
            string result = QueryValidator.Validate("SELECT * FROM users", new List<string> { "SELECT" });
            Assert.Null(result);
        }

        /// <summary>
        /// DELETE query is rejected when AllowedQueries only contains SELECT.
        /// </summary>
        [Fact]
        public void Validate_DeleteNotAllowed_ReturnsError()
        {
            string result = QueryValidator.Validate("DELETE FROM users", new List<string> { "SELECT" });
            Assert.NotNull(result);
            Assert.Contains("DELETE", result);
        }

        /// <summary>
        /// Leading whitespace is stripped before validation.
        /// </summary>
        [Fact]
        public void Validate_LeadingWhitespace_Stripped()
        {
            string result = QueryValidator.Validate("  SELECT 1", new List<string> { "SELECT" });
            Assert.Null(result);
        }

        /// <summary>
        /// Leading single-line SQL comments are stripped before validation.
        /// </summary>
        [Fact]
        public void Validate_LeadingSingleLineComment_Stripped()
        {
            string result = QueryValidator.Validate("-- comment\nSELECT 1", new List<string> { "SELECT" });
            Assert.Null(result);
        }

        /// <summary>
        /// Leading block comments are stripped before validation.
        /// </summary>
        [Fact]
        public void Validate_LeadingBlockComment_Stripped()
        {
            string result = QueryValidator.Validate("/* comment */ SELECT 1", new List<string> { "SELECT" });
            Assert.Null(result);
        }

        /// <summary>
        /// Matching is case-insensitive: lowercase "select" matches "SELECT" in the allowed list.
        /// </summary>
        [Fact]
        public void Validate_CaseInsensitive_Matches()
        {
            string result = QueryValidator.Validate("select * from users", new List<string> { "SELECT" });
            Assert.Null(result);
        }

        /// <summary>
        /// Empty AllowedQueries list rejects everything.
        /// </summary>
        [Fact]
        public void Validate_EmptyAllowedList_RejectsAll()
        {
            string result = QueryValidator.Validate("SELECT 1", new List<string>());
            Assert.NotNull(result);
        }

        /// <summary>
        /// INSERT query is allowed when AllowedQueries contains INSERT.
        /// </summary>
        [Fact]
        public void Validate_InsertAllowed_ReturnsNull()
        {
            string result = QueryValidator.Validate("INSERT INTO users VALUES (1)", new List<string> { "INSERT" });
            Assert.Null(result);
        }
    }
}
