using NServiceBus.AcceptanceTests.EndpointTemplates;

namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    public class CustomizedServer : DefaultServer
    {
        public CustomizedServer(string connectionString, bool supportsPublishSubscribe = true, bool supportsDelayedDelivery = true)
        {
            var transport = new SqlServerTransport(supportsDelayedDelivery, supportsPublishSubscribe)
            {
                ConnectionString = connectionString
            };

            TransportConfiguration = new ConfigureEndpointSqlServerTransport(transport);
        }
    }
}