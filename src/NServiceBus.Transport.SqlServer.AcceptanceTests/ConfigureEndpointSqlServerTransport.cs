﻿using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
using System.Text;
using System.Threading.Tasks;
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
        var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'SqlServerTransportConnectionString' environment variable is not set.");
        }

        transport = new SqlServerTransport {ConnectionString = connectionString};
        transport.Subscriptions.DisableSubscriptionCache();

#if !NETFRAMEWORK
        transport.TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
#endif
    }

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings runSettings, PublisherMetadata publisherMetadata)
    {
        doNotCleanNativeSubscriptions = runSettings.TryGet<bool>("DoNotCleanNativeSubscriptions", out _);

        configuration.UseTransport(transport);

        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        var connectionString = transport.ConnectionString;

        if (connectionString == null)
        {
            using (var connection = await transport.ConnectionFactory().ConfigureAwait(false))
            {
                connectionString = connection.ConnectionString;
            }
        }

        using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var queueAddresses = transport.Testing.ReceiveAddresses;
            var delayedQueueAddress = transport.Testing.DelayedDeliveryQueue;

            //TODO: this needs to be fixed
            if (queueAddresses == null) return;

            var commandTextBuilder = new StringBuilder();
            foreach (var address in queueAddresses)
            {
                commandTextBuilder.AppendLine($"IF OBJECT_ID('{address}', 'U') IS NOT NULL DROP TABLE {address}");
                commandTextBuilder.AppendLine($"IF OBJECT_ID('{delayedQueueAddress}', 'U') IS NOT NULL DROP TABLE {delayedQueueAddress}");
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