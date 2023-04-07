namespace TestSuite
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
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

        //[Test]
        //public async Task PingPong2()
        //{
        //    var result = await TestScenarioRunner.Run("Ping-Pong", new[]
        //    {
        //        new AgentInfo()
        //        {
        //            Behavior = "TestBehaviors.V8.Sender, TestBehaviors.V8",
        //            Project = "TestAgent.V8"
        //        },
        //        new AgentInfo()
        //        {
        //            Behavior = "TestBehaviors.V7.Receiver, TestBehaviors.V7",
        //            Project = "TestAgent.V7"
        //        },
        //    }).ConfigureAwait(false);
        //    Assert.True(result.Succeeded);

        //    /*
        //     * The spike implementation of the shared ITestContextAccessor is
        //     * based on a Dictionary<string, int>. It allows only for 0 or 1 named
        //     * flags. When setting the flag using the boolean overload, the value
        //     * will be converted to int. his is why it must be consumed as int. 
        //     */
        //    var requestReceived = (int)result.VariableValues["RequestReceived"];
        //    var responseReceived = (int)result.VariableValues["ResponseReceived"];

        //    Assert.AreEqual(1, requestReceived);
        //    Assert.AreEqual(1, responseReceived);
        //}
    }
}
