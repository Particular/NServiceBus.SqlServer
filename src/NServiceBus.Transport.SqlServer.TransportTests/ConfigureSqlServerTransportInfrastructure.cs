using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using NUnit.Framework;

public class ConfigureSqlServerTransportInfrastructure : IConfigureTransportInfrastructure
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") 
        ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

    public TransportDefinition CreateTransportDefinition() => new SqlServerTransport(ConnectionString);

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress queueAddress, string errorQueueName, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() && transportDefinition.TransportTransactionMode == TransportTransactionMode.TransactionScope)
        {
            Assert.Ignore("DTC does not work on Linux.");
        }

        sqlServerTransport = (SqlServerTransport)transportDefinition;

        inputQueueName = queueAddress.ToString();
        this.errorQueueName = errorQueueName;

        sqlServerTransport.DelayedDelivery.TableSuffix = "Delayed";
        sqlServerTransport.Subscriptions.DisableCaching = true;

        var receivers = new[]
        {
            new ReceiveSettings(
                "mainReceiver",
                queueAddress,
                transportDefinition.SupportsPublishSubscribe,
                false,
                errorQueueName)
        };

        return await sqlServerTransport.Initialize(hostSettings, receivers, new[] { errorQueueName }, cancellationToken).ConfigureAwait(false);
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        if (sqlServerTransport is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString) == false)
        {
            var queues = new[]
            {
                errorQueueName,
                inputQueueName,
                sqlServerTransport.Testing.DelayedDeliveryQueue
            };

            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync(cancellationToken);

                foreach (var queue in queues)
                {
                    if (string.IsNullOrWhiteSpace(queue) == false)
                    {
                        using (var comm = conn.CreateCommand())
                        {
                            comm.CommandText = $"IF OBJECT_ID('{queue}', 'U') IS NOT NULL DROP TABLE {queue}";

                            await comm.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }
                }
            }
        }
    }

    string inputQueueName;
    string errorQueueName;
    SqlServerTransport sqlServerTransport;
}