using System;
using System.Threading;
#if SYSTEMDATASQLCLIENT
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using NUnit.Framework;

public class ConfigureSqlServerTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

        return new SqlServerTransport(connectionString);
    }

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
                true,
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

        if (string.IsNullOrWhiteSpace(connectionString) == false)
        {
            var queues = new[]
            {
                errorQueueName,
                inputQueueName,
                sqlServerTransport.Testing.DelayedDeliveryQueue
            };

            using (var conn = new SqlConnection(connectionString))
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

    string connectionString;
    string inputQueueName;
    string errorQueueName;
    SqlServerTransport sqlServerTransport;
}