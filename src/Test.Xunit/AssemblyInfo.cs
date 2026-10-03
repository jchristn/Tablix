// The shared suites assert on process-wide telemetry (one Tablix meter and activity source per process) and on
// static gauge providers, so the Fact and Theory hosts must not run their suites concurrently.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
