namespace TestSuite
{
    using System.Linq;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using TestRunner;

    [TestFixture]
    public class ThisIsATest
    {
        [Test]
        [TestCase("V8", "V8")]
        [TestCase("V7", "V7")]
        [TestCase("V8", "V7")]
        [TestCase("V7", "V8")]
        [Repeat(3)]
        public async Task PingPong(string v1, string v2)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var agents = new[]
            {
                AgentInfo.Create(v1, "Sender"),
                AgentInfo.Create(v2, "Receiver"),
            };

            var result = await TestScenarioPluginRunner.Run("Ping-Pong", agents, x => x.Count == 2, cts.Token).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Send");
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Reply");

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);
        }
    }
}
