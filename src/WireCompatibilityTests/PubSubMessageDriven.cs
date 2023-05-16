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
    public class PubSubMessageDriven
    {
        [Test]
        [TestCaseSource(typeof(GeneratedVersionsSet), nameof(GeneratedVersionsSet.Get), new object[] { "[4,5)" })]
        public async Task Simple(NuGetVersion a, NuGetVersion b)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run(
                "MessageDrivenSubscriber",
                "MessageDrivenPublisher",
                a,
                b,
                x => x.Count == 1,
                cts.Token
                )
                .ConfigureAwait(false);

            Assert.True(result.Succeeded);
            Assert.AreEqual(1, result.AuditedMessages.Count, "Audit queue message count");
            Assert.True(result.AuditedMessages.Values.All(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Publish)), "No event message in audit queue");

            var eventVersion = SemanticVersion.Parse(result.AuditedMessages.Values.First().Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(b, eventVersion);
        }
    }
}
