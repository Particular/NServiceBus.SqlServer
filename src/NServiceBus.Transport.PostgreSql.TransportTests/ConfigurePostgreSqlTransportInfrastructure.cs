using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using NServiceBus;
using NServiceBus.Transport;
using NServiceBus.TransportTests;
using QueueAddress = NServiceBus.Transport.QueueAddress;

public class ConfigurePostgreSqlTransportInfrastructure : IConfigureTransportInfrastructure
{
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PostgreSqlTransportConnectionString") ??
        @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Connection Lifetime=0;";

    public TransportDefinition CreateTransportDefinition() => new PostgreSqlTransport(ConnectionString);

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

    public string GetInputQueueName(string testName, TransportTransactionMode transactionMode)
    {
        var fullTestName = $"{testName}{transactionMode}";
        var fullTestNameHash = CreateDeterministicHash(fullTestName);

        // Max length for table name is 63. We need to reserve space for the ".delayed" suffix (8), the hashcode (8), and "_seq_seq" sequence suffix: 63-8-8-8=39
        var charactersToConsider = int.Min(fullTestName.Length, 39);

        return $"{fullTestName[..charactersToConsider]}{fullTestNameHash:X8}";
    }

    public string GetErrorQueueName(string testName, TransportTransactionMode transactionMode)
    {
        var fullTestName = $"{testName}{transactionMode}";
        var fullTestNameHash = CreateDeterministicHash(fullTestName);

        // Max length for table name is 63. We need to reserve space for the ".error" suffix (6) the hashcode (8), and "_seq_seq" sequence suffix: 63-8-6-8=41
        var charactersToConsider = int.Min(fullTestName.Length, 41);

        return $"{fullTestName[..charactersToConsider]}_error{fullTestNameHash:X8}";
    }

    static uint CreateDeterministicHash(string input)
    {
        var inputBytes = Encoding.Default.GetBytes(input);
        var hashBytes = MD5.HashData(inputBytes);

        return BitConverter.ToUInt32(hashBytes, 0) % 1000000;
    }

    public async Task Cleanup(CancellationToken cancellationToken = default)
    {
        if (postgreSqlTransport is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
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

        using var conn = new NpgsqlConnection(ConnectionString);
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

    string inputQueueName;
    string errorQueueName;
    PostgreSqlTransport postgreSqlTransport;
}