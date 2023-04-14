namespace TestSuite
{
    using System.Linq;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using TestRunner;

    [TestFixture]
    public class ThisIsATest
    {
        [Test]
        public async Task PingPong()
        {
            var result = await TestScenarioRunner.Run("Ping-Pong", new[]
            {
                new AgentInfo()
                {
                    Behavior = "WireCompatibilityTests.TestBehaviors.V7.Sender, WireCompatibilityTests.TestBehaviors.V7",
                    Project = "WireCompatibilityTests.Generated.TestAgent.V7"
                },
                new AgentInfo()
                {
                    Behavior = "WireCompatibilityTests.TestBehaviors.V8.Receiver, WireCompatibilityTests.TestBehaviors.V8",
                    Project = "WireCompatibilityTests.Generated.TestAgent.V8"
                },
            }, x => x.Count == 2).ConfigureAwait(false);

            Assert.True(result.Succeeded);

            var request = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Send");
            var response = result.AuditedMessages.Values.Single(x => x.Headers[Headers.MessageIntent] == "Reply");

            Assert.AreEqual(request.Headers[Headers.MessageId], response.Headers[Headers.RelatedTo]);
            Assert.AreEqual(request.Headers[Headers.ConversationId], response.Headers[Headers.ConversationId]);
            Assert.AreEqual(request.Headers[Headers.CorrelationId], response.Headers[Headers.CorrelationId]);
        }
    }
}
