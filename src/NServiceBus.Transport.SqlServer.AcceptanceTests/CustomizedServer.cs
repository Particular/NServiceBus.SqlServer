namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public class CustomizedServer : DefaultServer
    {
        public CustomizedServer(SqlServerTransport transport)
        {
            TransportConfiguration = new ConfigureEndpointSqlServerTransport(transport);
        }

        public CustomizedServer(string connectionString, bool supportsPublishSubscribe = true, bool supportsDelayedDelivery = true)
        {
            var transport = new SqlServerTransport(connectionString, supportsDelayedDelivery, supportsPublishSubscribe);

            TransportConfiguration = new ConfigureEndpointSqlServerTransport(transport);
        }
    }
}