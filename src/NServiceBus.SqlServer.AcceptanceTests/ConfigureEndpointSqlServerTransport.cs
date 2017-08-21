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
                TryDeleteTable(conn, new QueueAddress(address.TableName.Trim('[',']') + ".Delayed", address.SchemaName));
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
                comm.CommandText = $"IF OBJECT_ID('{address}', 'U') IS NOT NULL DROP TABLE {address}";
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

    class QueueAddress
    {
        public QueueAddress(string tableName, string schemaName)
        {
            TableName = NameHelper.Quote(tableName);
            SchemaName = NameHelper.Quote(schemaName);
        }

        public string TableName { get; }
        public string SchemaName { get; }

        public static QueueAddress Parse(string address)
        {
            var firstAtIndex = address.IndexOf("@", StringComparison.Ordinal);

            if (firstAtIndex == -1)
            {
                return new QueueAddress(address, null);
            }

            var tableName = address.Substring(0, firstAtIndex);
            address = firstAtIndex + 1 < address.Length ? address.Substring(firstAtIndex + 1) : string.Empty;

            var schemaName = ExtractSchema(address);
            return new QueueAddress(tableName, schemaName);
        }

        static string ExtractSchema(string address)
        {
            var noRightBrackets = 0;
            var index = 1;

            while (true)
            {
                if (index >= address.Length)
                {
                    return address;
                }
                if (address[index] == '@' && (address[0] != '[' || noRightBrackets % 2 == 1))
                {
                    return address.Substring(0, index);
                }

                if (address[index] == ']')
                {
                    noRightBrackets++;
                }
                index++;
            }
        }

        public override string ToString()
        {
            if (SchemaName == null)
            {
                return TableName;
            }
            return $"{SchemaName}.{TableName}";
        }
    }
}