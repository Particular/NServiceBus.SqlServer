namespace TestSuite
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;
    using NUnit.Framework;
    using TestRunner;

    [TestFixture]
    public class ThisIsATest
    {
        [Test]
        [TestCaseSource(typeof(TestCaseGenerator))]
        public async Task PingPong(string v1, int core1, string v2, int core2)
        {
            var settings = new Dictionary<string, string> { ["ConnectionString"] = "Data source = (local); Initial catalog = WireCompat; Integrated Security = true; Encrypt=false" };

            using var cts = new CancellationTokenSource();
            var agents = new[]
            {
                AgentInfo.Create("Sender", v1, core1, settings),
                AgentInfo.Create("Receiver", v2, core2, settings),
            };

            var result = await TestScenarioPluginRunner.Run("Ping-Pong", agents, x => x.Count == 2, cts.Token).ConfigureAwait(false);

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

#pragma warning disable CA1710
    public class TestCaseGenerator : IEnumerable
#pragma warning restore CA1710
    {
        readonly Dictionary<string, int> includedVersions = new()
        {
            //{"4.0", 7},
            //{"4.1", 7},
            //{"4.2", 7},
            {"4.3", 7},
            {"5.0", 7},
            //{"6.0", 7},
            //{"6.1", 7},
            //{"6.2", 7},
            {"6.3", 7},
            {"7.0", 8},
        };

        public IEnumerator GetEnumerator()
        {
            foreach (var v1 in includedVersions)
            {
                foreach (var v2 in includedVersions)
                {
                    if (v1.Key != v2.Key)
                    {
                        yield return new object[] { v1.Key, v1.Value, v2.Key, v2.Value };
                    }
                }
            }
        }
    }
}
