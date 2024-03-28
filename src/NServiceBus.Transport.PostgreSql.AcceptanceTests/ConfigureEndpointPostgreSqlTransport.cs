using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport.PostgreSql;
using NServiceBus.Transport.SqlServer;

public class ConfigureEndpointPostgreSqlTransport : IConfigureEndpointTestExecution
{
    public ConfigureEndpointPostgreSqlTransport(PostgreSqlTransport transport)
    {
        this.transport = transport;
    }

    public ConfigureEndpointPostgreSqlTransport()
    {
        connectionString = Environment.GetEnvironmentVariable("PostgreSqlTransportConnectionString") ?? @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Connection Lifetime=0;Include Error Detail=true";

        transport = new PostgreSqlTransport(connectionString);
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
        this.endpointName = endpointName;

        return Task.FromResult(0);
    }

    public async Task Cleanup()
    {
        //TODO: clean-up sequences
        using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var queueAddresses = transport.Testing.ReceiveAddresses;
            var delayedQueueAddress = transport.Testing.DelayedDeliveryQueue;

            var commandTextBuilder = new StringBuilder();

            var nameHelper = new PostgreSqlNameHelper();

            //No clean-up for send-only endpoints
            if (queueAddresses != null)
            {
                foreach (var address in queueAddresses)
                {
                    commandTextBuilder.AppendLine($"DROP TABLE IF EXISTS {address};");

                    //We want to get the sequence name from the table name e.g. "public"."something" -> "public"."something_seq_seq"

                    var tableName = nameHelper.Unquote(address.Replace("\"public\".", string.Empty));
                    var sequenceName = $"\"public\".\"{tableName}_seq_seq\"";

                    commandTextBuilder.AppendLine($"DROP SEQUENCE IF EXISTS {sequenceName};");
                }
            }

            //Null-check because if an exception is thrown before startup these fields might be empty
            if (delayedQueueAddress != null)
            {
                commandTextBuilder.AppendLine($"DROP TABLE IF EXISTS {delayedQueueAddress};");

                //We want to get the sequence name from the table name e.g. "public"."something" -> "public"."something_seq_seq"

                var tableName = nameHelper.Unquote(delayedQueueAddress.Replace("\"public\".", string.Empty));
                var sequenceName = $"\"public\".\"{tableName}_seq_seq\"";

                commandTextBuilder.AppendLine($"DROP SEQUENCE IF EXISTS {sequenceName};");
            }

            var subscriptionTableName = transport.Testing.SubscriptionTable;

            if (!doNotCleanNativeSubscriptions && !string.IsNullOrEmpty(subscriptionTableName))
            {
                commandTextBuilder.AppendLine($"DROP TABLE IF EXISTS {subscriptionTableName};");
            }

            var commandText = commandTextBuilder.ToString();
            if (!string.IsNullOrEmpty(commandText))
            {
                await TryDeleteTables(conn, commandText);
            }
        }
    }

    async Task TryDeleteTables(NpgsqlConnection conn, string commandText)
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
                throw new Exception($"Failed to execute query in {endpointName}: {commandText}", e);
            }
        }
    }

    bool doNotCleanNativeSubscriptions;
    PostgreSqlTransport transport;
    string connectionString;
    string endpointName;
}