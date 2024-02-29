﻿using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

public class ConfigureEndpointSqlServerTransport : IConfigureEndpointTestExecution
{
    public ConfigureEndpointSqlServerTransport(SqlServerTransport transport)
    {
        this.transport = transport;
    }

    public ConfigureEndpointSqlServerTransport()
    {
        var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

        transport = new SqlServerTransport(connectionString);
        transport.Subscriptions.DisableCaching = true;

        //On non windows operating systems we need to explicitly set the transaction mode to SendsAtomicWithReceive since distributed transactions is not available there
        if (!OperatingSystem.IsWindows() && transport.TransportTransactionMode == TransportTransactionMode.TransactionScope)
        {
            transport.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
        }
    }

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings runSettings, PublisherMetadata publisherMetadata)
    {
        doNotCleanNativeSubscriptions = runSettings.TryGet<bool>("DoNotCleanNativeSubscriptions", out _);

        configuration.UseTransport(transport);

        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        Func<Task<SqlConnection>> factory = async () =>
        {
            if (transport.ConnectionString != null)
            {
                var connection = new SqlConnection(transport.ConnectionString);
                await connection.OpenAsync().ConfigureAwait(false);
                return connection;
            }

            return await transport.ConnectionFactory(CancellationToken.None).ConfigureAwait(false);
        };

        using (var conn = await factory().ConfigureAwait(false))
        {
            var queueAddresses = transport.Testing.ReceiveAddresses;
            var delayedQueueAddress = transport.Testing.DelayedDeliveryQueue;

            var commandTextBuilder = new StringBuilder();

            //No clean-up for send-only endpoints
            if (queueAddresses != null)
            {
                foreach (var address in queueAddresses)
                {
                    commandTextBuilder.AppendLine($"IF OBJECT_ID('{address}', 'U') IS NOT NULL DROP TABLE {address}");
                    commandTextBuilder.AppendLine(
                        $"IF OBJECT_ID('{delayedQueueAddress}', 'U') IS NOT NULL DROP TABLE {delayedQueueAddress}");
                }
            }

            var subscriptionTableName = transport.Testing.SubscriptionTable;

            if (!doNotCleanNativeSubscriptions && !string.IsNullOrEmpty(subscriptionTableName))
            {
                commandTextBuilder.AppendLine($"IF OBJECT_ID('{subscriptionTableName}', 'U') IS NOT NULL DROP TABLE {subscriptionTableName}");
            }

            var commandText = commandTextBuilder.ToString();
            if (!string.IsNullOrEmpty(commandText))
            {
                await TryDeleteTables(conn, commandText);
            }
        }
    }

    static async Task TryDeleteTables(SqlConnection conn, string commandText)
    {
        try
        {
            using (var comm = conn.CreateCommand())
            {
                comm.CommandText = commandText;
                await comm.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            if (!e.Message.Contains("it does not exist or you do not have permission"))
            {
                throw;
            }
        }
    }

    bool doNotCleanNativeSubscriptions;
    SqlServerTransport transport;
}