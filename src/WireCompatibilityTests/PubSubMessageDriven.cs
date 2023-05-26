namespace TestSuite
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NuGet.Versioning;
    using NUnit.Framework;
    using WireCompatibilityTests;

    [Parallelizable(ParallelScope.All)]
    [TestFixture]
    public class PubSubMessageDriven
    {
        [Test]
        [TestCaseSourcePackageSupportedVersions("NServiceBus.SqlServer", "[4,6)")]
        public async Task Simple(NuGetVersion publisherVersion, NuGetVersion subscriberVersion)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run(
                "MessageDrivenPublisher",
                "MessageDrivenSubscriber",
                publisherVersion,
                subscriberVersion,
                x => x.Count == 1,
                cts.Token
                )
                .ConfigureAwait(false);

            Assert.True(result.Succeeded);
            Assert.AreEqual(1, result.AuditedMessages.Count, "Audit queue message count");
            Assert.True(result.AuditedMessages.Values.All(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Publish)), "No event message in audit queue");

            var eventVersion = SemanticVersion.Parse(result.AuditedMessages.Values.First().Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(publisherVersion, eventVersion);
        }
    }
}
