namespace WireCompatibilityTests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestRunner;

public static class ScenarioRunner
{
    public static async Task<TestExecutionResult> Run(string name, string behavior1, string behavior2, string v1, int core1, string v2, int core2, Func<Dictionary<string, AuditMessage>, bool> doneCallback, CancellationToken cancellationToken = default)
    {
        var platformSpecificAssemblies = new Dictionary<string, string>
        {
            ["Microsoft.Data.SqlClient"] = "net6.0",
            ["System.Data.SqlClient"] = "netcoreapp2.1"
        };

        var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString")
                               ?? "Data source = (local); Initial catalog = nservicebus; Integrated Security = true; Encrypt=false";

        var auditSpyTransport = new SqlServerTransport(connectionString)
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly,
        };

        var settings = new Dictionary<string, string> { ["ConnectionString"] = connectionString };

        var agents = new[]
        {
            AgentInfo.Create(behavior1, v1, core1, settings),
            AgentInfo.Create(behavior2, v2, core2, settings),
        };

        var result = await TestScenarioPluginRunner
            .Run(name, agents, auditSpyTransport, platformSpecificAssemblies, doneCallback, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }
}