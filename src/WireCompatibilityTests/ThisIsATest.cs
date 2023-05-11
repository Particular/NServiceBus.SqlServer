namespace WireCompatibilityTests
{
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;

    [TestFixture]
    public class ThisIsATest
    {
        [Test]
        [TestCaseSource(typeof(TestCaseGenerator))]
        public async Task SingleSchemaRequestReply(string v1, int core1, string v2, int core2)
        {
            var result = await ScenarioRunner.Run("Single-schema request-reply", "Sender", "Receiver", v1, core1, v2, core2, x => x.Count == 2).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Send");
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Reply");

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);
            Assert.AreEqual(v1, request.Headers["WireCompatVersion"]);

            Assert.AreEqual(v2, response.Headers["WireCompatVersion"]);
        }

        [Test]
        [TestCaseSource(typeof(TestCaseGenerator))]
        public async Task MultiSchemaRequestReply(string v1, int core1, string v2, int core2)
        {
            var result = await ScenarioRunner.Run("Multi-schema request-reply", "SchemaSender", "SchemaReceiver", v1, core1, v2, core2, x => x.Count == 2).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Send");
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Reply");

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);
            Assert.AreEqual(v1, request.Headers["WireCompatVersion"]);

            Assert.AreEqual(v2, response.Headers["WireCompatVersion"]);
        }
    }
}
