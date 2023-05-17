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
    public class PingPong
    {
        [OneTimeSetUp]
        public async Task SetUp()
        {
            await SqlHelper.CreateSchema(Global.ConnectionString, "receiver").ConfigureAwait(false);
            await SqlHelper.CreateSchema(Global.ConnectionString, "sender").ConfigureAwait(false);
        }

        [Test]
        [TestCaseSource(typeof(GeneratedVersionsSet), nameof(GeneratedVersionsSet.Get), new object[] { "[4.0.0,)" })]
        public async Task SingleSchemaRequestReply(NuGetVersion v1, NuGetVersion v2)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run("Sender", "Receiver", v1, v2, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            Assert.AreEqual(2, result.AuditedMessages.Values.Count, "Number of messages in audit queue");

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Send));
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Reply));

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);

            var requestVersion = SemanticVersion.Parse(request.Headers[Keys.WireCompatVersion]);
            var responseVersion = SemanticVersion.Parse(response.Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(v1, requestVersion);
            Assert.AreEqual(v2, responseVersion);
        }

        [Test]
        [TestCaseSource(typeof(GeneratedVersionsSet), nameof(GeneratedVersionsSet.Get), new object[] { "[6.0.0,)" })]
        public async Task MultiSchemaRequestReply(NuGetVersion v1, NuGetVersion v2)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run("SchemaSender", "SchemaReceiver", v1, v2, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Send));
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Reply));

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);

            var requestVersion = SemanticVersion.Parse(request.Headers[Keys.WireCompatVersion]);
            var responseVersion = SemanticVersion.Parse(response.Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(v1, requestVersion);
            Assert.AreEqual(v2, responseVersion);
        }
    }
}
