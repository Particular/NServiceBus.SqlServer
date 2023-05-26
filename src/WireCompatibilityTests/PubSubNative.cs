namespace TestSuite
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NuGet.Versioning;
    using NUnit.Framework;
    using WireCompatibilityTests;

    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class PubSubNative
    {
        [Test]
        [TestCaseSourcePackageSupportedVersions("NServiceBus.SqlServer", "[5,)")]
        public async Task Simple(NuGetVersion subscriberVersion, NuGetVersion publisherVersion)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run(
                "Subscriber",
                "Publisher",
                subscriberVersion,
                publisherVersion,
                x => x.Count == 2,
                cts.Token
                )
                .ConfigureAwait(false);

            Assert.True(result.Succeeded);
            Assert.AreEqual(2, result.AuditedMessages.Count, "Audit queue message count");
            Assert.True(result.AuditedMessages.All(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Publish)), "No event message in audit queue");

            var eventVersion = SemanticVersion.Parse(result.AuditedMessages.First().Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(publisherVersion, eventVersion);
        }
    }
}
