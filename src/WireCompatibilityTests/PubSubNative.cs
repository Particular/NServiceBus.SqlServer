namespace TestSuite
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using WireCompatibilityTests;

    [TestFixture]
    public class PubSubNative
    {
        [Test]
        [TestCaseSource(typeof(TestCaseGenerator))]
        public async Task Simple(string v1, int core1, string v2, int core2)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run("Subscriber", "Publisher", v1, core1, v2, core2, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);
            Assert.AreEqual(2, result.AuditedMessages.Count, "Audit queue message count");
            Assert.True(result.AuditedMessages.Values.All(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Publish)), "No event message in audit queue");
        }
    }
}
