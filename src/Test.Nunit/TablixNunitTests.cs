namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Data-driven Touchstone NUnit host for Tablix.
    /// </summary>
    [TestFixture]
    public sealed class TablixNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(TablixSuites.All);
        }

        /// <summary>
        /// Run one shared descriptor.
        /// </summary>
        /// <param name="testCase">Test case descriptor.</param>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
