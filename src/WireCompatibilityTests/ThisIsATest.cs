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
        public async Task PingPong(string x, string y)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var agents = new[]
            {
                AgentInfo.Create(x, "Sender"),
                AgentInfo.Create(y, "Receiver"),
            };

            var result = await TestScenarioPluginRunner.Run("Ping-Pong", agents, cts.Token).ConfigureAwait(false);

            //var audit = auditSpy.ProcessAuditQueue();

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Send");
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Reply");

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);
        }
    }
}
