namespace WireCompatibilityTests;

using System;
using System.Collections.Generic;
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
        string aTypeNameBehavior,
        string bTypeNameBehavior,
        SemanticVersion a,
        SemanticVersion b,
        Func<List<AuditMessage>, bool> doneCallback,
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
                AgentInfo.Create(aTypeNameBehavior, a, opts),
                AgentInfo.Create(bTypeNameBehavior, b, opts),
            };

            var result = await TestScenarioPluginRunner
                .Run(opts, agents, auditSpyTransport, platformSpecificAssemblies, doneCallback, cancellationToken)
                .ConfigureAwait(false);

            result.AuditedMessages = result.AuditedMessages;
            return result;
        }
        finally
        {
            Pool.Return(runCount);
        }
    }
}