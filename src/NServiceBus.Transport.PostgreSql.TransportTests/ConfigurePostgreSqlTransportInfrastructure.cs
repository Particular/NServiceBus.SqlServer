using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.Transport.PostgreSql;
using NServiceBus.TransportTests;
using NUnit.Framework;
using QueueAddress = NServiceBus.Transport.QueueAddress;

public class ConfigurePostgreSqlTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportDefinition CreateTransportDefinition()
    {
        connectionString = Environment.GetEnvironmentVariable("PostgreSqlTransportConnectionString") ?? @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Connection Lifetime=0;";

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

            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            foreach (var queue in queues)
            {
                if (string.IsNullOrWhiteSpace(queue) == false)
                {
                    using (var comm = conn.CreateCommand())
                    {
                        comm.CommandText = $"DROP TABLE IF EXISTS {queue}";

                        //await comm.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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