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
    public class PubSubNative
    {
        [Test]
        [TestCaseSource(typeof(GeneratedVersionsSet), nameof(GeneratedVersionsSet.Get), new object[] { "[6.0.0,)" })]
        public async Task Simple(NuGetVersion v1, NuGetVersion v2)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run(
                "Subscriber",
                "Publisher",
                v1,
                v2,
                x => x.Count == 2,
                cts.Token
                )
                .ConfigureAwait(false);

            Assert.True(result.Succeeded);
            Assert.AreEqual(2, result.AuditedMessages.Count, "Audit queue message count");
            Assert.True(result.AuditedMessages.Values.All(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Publish)), "No event message in audit queue");

            var eventVersion = SemanticVersion.Parse(result.AuditedMessages.Values.First().Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(v2, eventVersion);
        }
    }
}
