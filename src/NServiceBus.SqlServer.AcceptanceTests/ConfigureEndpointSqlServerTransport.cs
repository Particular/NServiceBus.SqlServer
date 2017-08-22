using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Transport;
using NServiceBus.Transport.SQLServer;

public class ConfigureEndpointSqlServerTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        queueBindings = configuration.GetSettings().Get<QueueBindings>();

        connectionString = Environment.GetEnvironmentVariable("SqlServerTransport.ConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'SqlServerTransport.ConnectionString' environment variable is not set.");
        }

        var transportConfig = configuration.UseTransport<SqlServerTransport>();
        transportConfig.ConnectionString(connectionString);

#if !NET452
        transportConfig.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
#endif

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
        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();

            var queueAddresses = queueBindings.ReceivingAddresses.Select(QueueAddress.Parse).ToList();
            foreach (var address in queueAddresses)
            {
                TryDeleteTable(conn, address);
                TryDeleteTable(conn, new QueueAddress(address.Table + ".Delayed", address.Schema, address.Catalog));
            }
        }
        return Task.FromResult(0);
    }

    static void TryDeleteTable(SqlConnection conn, QueueAddress address)
    {
        try
        {
            using (var comm = conn.CreateCommand())
            {
                comm.CommandText = $"IF OBJECT_ID('{address.QualifiedTableName}', 'U') IS NOT NULL DROP TABLE {address.QualifiedTableName}";
                comm.ExecuteNonQuery();
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

    string connectionString;
    QueueBindings queueBindings;
}