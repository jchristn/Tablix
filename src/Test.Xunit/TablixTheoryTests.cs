namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using global::Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// Theory-style Touchstone test host for Tablix.
    /// </summary>
    public sealed class TablixTheoryTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Instantiate with xUnit output helper.
        /// </summary>
        /// <param name="output">Test output helper.</param>
        public TablixTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// Provide non-skipped Touchstone test cases.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in TablixSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Provide skipped Touchstone test cases.
        /// </summary>
        /// <returns>Theory data.</returns>
        public static TheoryData<TestCaseDescriptor> SkippedCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in TablixSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (testCase.Skip)
                        data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Run one shared descriptor.
        /// </summary>
        /// <param name="testCase">Test case descriptor.</param>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }

        /// <summary>
        /// Report skipped shared descriptors through xUnit.
        /// </summary>
        /// <param name="testCase">Skipped test case descriptor.</param>
        [Theory(Skip = "Dynamically skipped Touchstone test cases")]
        [MemberData(nameof(SkippedCases))]
        public Task Skipped(TestCaseDescriptor testCase)
        {
            _Output.WriteLine("Skipped: " + testCase.SkipReason);
            return Task.CompletedTask;
        }
    }
}
