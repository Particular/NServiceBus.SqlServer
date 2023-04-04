namespace TestSuite
{
    using System.Threading.Tasks;
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
            }).ConfigureAwait(false);

            //var audit = auditSpy.ProcessAuditQueue();

            Assert.True(result.Succeeded);

            /*
             * The spike implementation of the shared ITestContextAccessor is
             * based on a Dictionary<string, int>. It allows only for 0 or 1 named
             * flags. When setting the flag using the boolean overload, the value
             * will be converted to int. his is why it must be consumed as int. 
             */
            var requestReceived = (int)result.VariableValues["RequestReceived"];
            var responseReceived = (int)result.VariableValues["ResponseReceived"];

            Assert.AreEqual(1, requestReceived);
            Assert.AreEqual(1, responseReceived);
        }

        [Test]
        public async Task PingPong2()
        {
            var result = await TestScenarioRunner.Run("Ping-Pong", new[]
            {
                new AgentInfo()
                {
                    Behavior = "TestBehaviors.V8.Sender, TestBehaviors.V8",
                    Project = "TestAgent.V8"
                },
                new AgentInfo()
                {
                    Behavior = "TestBehaviors.V7.Receiver, TestBehaviors.V7",
                    Project = "TestAgent.V7"
                },
            }).ConfigureAwait(false);
            Assert.True(result.Succeeded);

            /*
             * The spike implementation of the shared ITestContextAccessor is
             * based on a Dictionary<string, int>. It allows only for 0 or 1 named
             * flags. When setting the flag using the boolean overload, the value
             * will be converted to int. his is why it must be consumed as int. 
             */
            var requestReceived = (int)result.VariableValues["RequestReceived"];
            var responseReceived = (int)result.VariableValues["ResponseReceived"];

            Assert.AreEqual(1, requestReceived);
            Assert.AreEqual(1, responseReceived);
        }
    }
}
