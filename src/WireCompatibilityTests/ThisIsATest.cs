namespace TestSuite
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NuGet.Common;
    using NuGet.Protocol.Core.Types;
    using NuGet.Protocol;
    using NUnit.Framework;
    using TestRunner;
    using NuGet.Versioning;
    using System.Linq;


    // How about NServiceBus.SqlServer vs NServiceBus.Transport.SqlServer ?


    [TestFixture]
    public class ThisIsATest
    {
        static IEnumerable<object[]> Versions()
        {
            using var nugetcache = new SourceCacheContext { NoCache = true };

            //string source = "https://www.myget.org/F/particular/api/v3/index.json";
            string source = "https://api.nuget.org/v3/index.json";
            var nuget = Repository.Factory.GetCoreV3(source);
            var resources = nuget.GetResource<FindPackageByIdResource>();

            var versions = resources.GetAllVersionsAsync("NServiceBus.SqlServer", nugetcache, NullLogger.Instance, CancellationToken.None).GetAwaiter().GetResult();

            // Get all minors
            versions = versions.Where(v => !v.IsPrerelease && v.Major >= 6).OrderBy(v => v);

            NuGetVersion last = null;

            var latestMinors = new HashSet<NuGetVersion>();

            foreach (var v in versions)
            {
                if (last == null)
                {
                    last = v;
                    continue;
                }

                if (last.Major != v.Major)
                {
                    latestMinors.Add(last);
                }
                else if (last.Minor != v.Minor)
                {
                    latestMinors.Add(last);
                }

                last = v;
            }

            latestMinors.Add(last);

            foreach (var a in latestMinors)
            {
                foreach (var b in latestMinors)
                {
                    yield return new object[] { a, b };
                }
            }
        }

        [Test]
        [TestCaseSource(nameof(Versions))]
        [Repeat(3)]
        public async Task PingPong(NuGetVersion a, NuGetVersion b)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var agents = new[]
            {
                AgentInfo.Create(a.Major, "Sender"),
                AgentInfo.Create(b.Major, "Receiver"),
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
