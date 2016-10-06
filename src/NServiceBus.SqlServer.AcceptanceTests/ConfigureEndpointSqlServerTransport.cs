using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests.ScenarioDescriptors;
using NServiceBus.Configuration.AdvanceExtensibility;
using NServiceBus.Transport;

public class ConfigureScenariosForSqlServerTransport : IConfigureSupportedScenariosForTestExecution
{
    public IEnumerable<Type> UnsupportedScenarioDescriptorTypes { get; } = new[]
    {
        typeof(AllTransportsWithCentralizedPubSubSupport),
        typeof(AllTransportsWithoutNativeDeferral),
        typeof(AllTransportsWithoutNativeDeferralAndWithAtomicSendAndReceive)
    };
}

public class ConfigureEndpointSqlServerTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings)
    {
        queueBindings = configuration.GetSettings().Get<QueueBindings>();
        connectionString = settings.Get<string>("Transport.ConnectionString");
        configuration.UseTransport<SqlServerTransport>().ConnectionString(connectionString);
        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        var queueNames = new List<string>();

        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();

            var qn = queueBindings.ReceivingAddresses.ToList();
            qn.ForEach(n =>
            {
                var nameParts = n.Split('@');
                if (nameParts.Length == 2)
                {
                    using (var sanitizer = new SqlCommandBuilder())
                    {
                        var sanitizedSchemaName = SanitizeIdentifier(nameParts[1], sanitizer);
                        var sanitizedTableName = SanitizeIdentifier(nameParts[0], sanitizer);

                        queueNames.Add($"{sanitizedSchemaName}.{sanitizedTableName}");
                    }
                }
                else
                {
                    queueNames.Add(n);
                }
            });
            foreach (var queue in queueNames)
            {
                using (var comm = conn.CreateCommand())
                {
                    comm.CommandText = $"IF OBJECT_ID('{queue}', 'U') IS NOT NULL DROP TABLE {queue}";
                    comm.ExecuteNonQuery();
                }
            }
        }

        return Task.FromResult(0);
    }

    static string SanitizeIdentifier(string identifier, SqlCommandBuilder sanitizer)
    {
        // Identifier may initially quoted or unquoted.
        return sanitizer.QuoteIdentifier(sanitizer.UnquoteIdentifier(identifier));
    }

    string connectionString;
    QueueBindings queueBindings;
}