using NServiceBus.AcceptanceTests.Audit;
using NUnit.Framework;

[assembly: Parallelizable(ParallelScope.Fixtures)]

// Both use the endpoint name audit_with_code_target.
[assembly: NonParallelizableFixtures(typeof(When_audit_is_overridden_in_code), typeof(When_audit_is_overridden_in_environment))]
