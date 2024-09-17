using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var queues = new List<string> { errorQueueName, inputQueueName };

        if (!postgreSqlTransport.DisableDelayedDelivery)
        {
            var delayedDeliveryQueueName = postgreSqlTransport.Testing.DelayedDeliveryQueue
                .Replace("\"public\".", string.Empty)
                .Replace("\"", string.Empty);

            queues.Add(delayedDeliveryQueueName);
        }

        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var queue in queues.Where(q => !string.IsNullOrWhiteSpace(q)))
        {
            using (var comm = conn.CreateCommand())
            {
                comm.CommandText = $"DROP TABLE IF EXISTS \"public\".\"{queue}\"; " +
                                   $"DROP SEQUENCE IF EXISTS \"public\".\"{queue}_seq_seq\";";

                await comm.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        var subscriptionTableName = postgreSqlTransport.Testing.SubscriptionTable;

        if (!string.IsNullOrEmpty(subscriptionTableName))
        {
            using (var comm = conn.CreateCommand())
            {
                comm.CommandText = $"DROP TABLE IF EXISTS {subscriptionTableName};";

                await comm.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    string connectionString;
    string inputQueueName;
    string errorQueueName;
    PostgreSqlTransport postgreSqlTransport;
}