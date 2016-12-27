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
        typeof(AllTransportsWithCentralizedPubSubSupport)
    };
}

public class ConfigureEndpointSqlServerTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        queueBindings = configuration.GetSettings().Get<QueueBindings>();
        connectionString = settings.Get<string>("Transport.ConnectionString");

        var transportConfig = configuration.UseTransport<SqlServerTransport>();
        
        transportConfig.ConnectionString(connectionString);

        var routingConfig = transportConfig.Routing();

        foreach (var publisher in publisherMetadata.Publishers)
        {
            foreach (var eventType in publisher.Events)
            {
                routingConfig.RegisterPublisher(eventType, publisher.PublisherName);
            }
        }

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
                if (nameParts.Length > 1)
                {
                    using (var sanitizer = new SqlCommandBuilder())
                    {
                        var sanitizedParts = nameParts.Reverse().Select(x => SanitizeIdentifier(x, sanitizer));
                        var queueName = string.Join(".", sanitizedParts);
                        queueNames.Add(queueName);
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