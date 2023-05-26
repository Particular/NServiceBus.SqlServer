namespace WireCompatibilityTests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NuGet.Versioning;
using TestRunner;
using TestSuite;

public static class ScenarioRunner
{
    public static long RunCounter;
    static readonly ObjectPool<long> Pool = new(() => Interlocked.Increment(ref RunCounter));

    public static async Task<TestExecutionResult> Run(
        string behavior1,
        string behavior2,
        SemanticVersion v1,
        SemanticVersion v2,
        Func<Dictionary<string, AuditMessage>, bool> doneCallback,
        CancellationToken cancellationToken = default
        )
    {
        var platformSpecificAssemblies = new Dictionary<string, string>
        {
            ["Microsoft.Data.SqlClient"] = "net6.0",
            ["System.Data.SqlClient"] = "netcoreapp2.1"
        };

        var connectionString = Global.ConnectionString;

        var runCount = Pool.Get();
        try
        {
            var testRunId = Guid.NewGuid().ToString();

            var opts = new PluginOptions
            {
                ConnectionString = Global.ConnectionString,
                TestRunId = testRunId,
                RunCount = runCount,
            };

            await SqlHelper.DropTablesWithPrefix(Global.ConnectionString, opts.ApplyUniqueRunPrefix(string.Empty), cancellationToken).ConfigureAwait(false);

            opts.AuditQueue = opts.ApplyUniqueRunPrefix("AuditSpy");

            var auditSpyTransport = new SqlServerTransport(connectionString + ";App=AuditSpy")
            {
                TransportTransactionMode = TransportTransactionMode.ReceiveOnly,
            };
            auditSpyTransport.Subscriptions.SubscriptionTableName = new NServiceBus.Transport.SqlServer.SubscriptionTableName(opts.ApplyUniqueRunPrefix("SubscriptionRouting"));

            var agents = new[]
            {
                AgentInfo.Create(behavior1, v1, opts),
                AgentInfo.Create(behavior2, v2, opts),
            };

            var result = await TestScenarioPluginRunner
                .Run(opts, agents, auditSpyTransport, platformSpecificAssemblies, doneCallback, cancellationToken)
                .ConfigureAwait(false);

            result.AuditedMessages = result.AuditedMessages
                .ToDictionary(x => x.Key, x => x.Value);
            return result;
        }
        finally
        {
            Pool.Return(runCount);
        }
    }
}