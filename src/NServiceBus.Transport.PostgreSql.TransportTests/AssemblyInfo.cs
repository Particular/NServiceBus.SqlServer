using NServiceBus.TransportTests;
using NUnit.Framework;

[assembly: Parallelizable(ParallelScope.Fixtures)]

// Asserts that nothing above Info was logged, but the transport log is shared by all fixtures.
[assembly: NonParallelizableFixtures(typeof(When_on_error_throws))]
