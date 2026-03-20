namespace Tablix.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Tablix.Core.Models;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="EnumerationResult{T}"/> pagination behavior using manual object construction.
    /// </summary>
    public class EnumerationTests
    {
        /// <summary>
        /// With 5 items, MaxResults=2, Skip=0: first 2 items returned, 3 remaining, not end of results.
        /// </summary>
        [Fact]
        public void Pagination_FirstPage_ReturnsCorrectSlice()
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
                EndOfResults = remaining <= 0
            };

            Assert.Equal(2, result.Objects.Count);
            Assert.Equal(3, result.RecordsRemaining);
            Assert.False(result.EndOfResults);
        }

        /// <summary>
        /// With 5 items, MaxResults=10, Skip=0: all 5 returned, 0 remaining, end of results.
        /// </summary>
        [Fact]
        public void Pagination_MaxResultsExceedsTotal_ReturnsAll()
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
                EndOfResults = remaining <= 0
            };

            Assert.Equal(5, result.Objects.Count);
            Assert.Equal(0, result.RecordsRemaining);
            Assert.True(result.EndOfResults);
        }

        /// <summary>
        /// With 5 items, MaxResults=2, Skip=4: 1 item returned, 0 remaining, end of results.
        /// </summary>
        [Fact]
        public void Pagination_LastPage_PartialResults()
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
                EndOfResults = remaining <= 0
            };

            Assert.Single(result.Objects);
            Assert.Equal(0, result.RecordsRemaining);
            Assert.True(result.EndOfResults);
        }
    }
}
