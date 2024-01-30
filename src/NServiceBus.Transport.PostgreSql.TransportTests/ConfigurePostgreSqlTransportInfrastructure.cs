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
using NServiceBus.Transport.PostgreSql;
using NServiceBus.TransportTests;
using NUnit.Framework;

public class ConfigurePostgreSqlTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        connectionString = Environment.GetEnvironmentVariable("PostgreSqlTransportConnectionString") ?? @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Min Pool Size=0;Max Pool Size=100;Connection Lifetime=0;";

        return new PostgreSqlTransport(connectionString);
    }

    public async Task<TransportInfrastructure> Configure(TransportDefinition transportDefinition, HostSettings hostSettings, QueueAddress queueAddress, string errorQueueName, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() && transportDefinition.TransportTransactionMode == TransportTransactionMode.TransactionScope)
        {
            Assert.Ignore("DTC does not work on Linux.");
        }

        postgreSqlTransport = (PostgreSqlTransport)transportDefinition;

        inputQueueName = queueAddress.ToString();
        this.errorQueueName = errorQueueName;

        postgreSqlTransport.DelayedDelivery.TableSuffix = "Delayed";
        postgreSqlTransport.Subscriptions.DisableCaching = true;

        var receivers = new[]
        {
            new ReceiveSettings(
                "mainReceiver",
                queueAddress,
                transportDefinition.SupportsPublishSubscribe,
                true,
                errorQueueName)
        };

        return await postgreSqlTransport.Initialize(hostSettings, receivers, new[] { errorQueueName }, cancellationToken).ConfigureAwait(false);
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        if (postgreSqlTransport is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(connectionString) == false)
        {
            var queues = new[]
            {
                errorQueueName,
                inputQueueName,
                postgreSqlTransport.Testing.DelayedDeliveryQueue
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
    PostgreSqlTransport postgreSqlTransport;
}