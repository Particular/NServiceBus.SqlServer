namespace TestSuite
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using WireCompatibilityTests;

    [TestFixture]
    public class PingPong
    {
        [Test]
        [TestCaseSource(typeof(TestCaseGenerator))]
        public async Task SingleSchemaRequestReply(string v1, int core1, string v2, int core2)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run("Sender", "Receiver", v1, core1, v2, core2, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            Assert.AreEqual(2, result.AuditedMessages.Values.Count, "Number of messages in audit queue");

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Send));
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Reply));

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);
            Assert.AreEqual(v1, request.Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(v2, response.Headers[Keys.WireCompatVersion]);
        }


        [Test]
        [TestCaseSource(typeof(TestCaseGenerator))]
        public async Task MultiSchemaRequestReply(string v1, int core1, string v2, int core2)
        {
            using var cts = new CancellationTokenSource(Global.TestTimeout);
            var result = await ScenarioRunner.Run("SchemaSender", "SchemaReceiver", v1, core1, v2, core2, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Send));
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == nameof(MessageIntent.Reply));

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);
            Assert.AreEqual(v1, request.Headers[Keys.WireCompatVersion]);
            Assert.AreEqual(v2, response.Headers[Keys.WireCompatVersion]);
        }
    }
}
