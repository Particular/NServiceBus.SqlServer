namespace NServiceBus.Transport.PostgreSql.AcceptanceTests
{
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using PostgreSql;

    public class CustomizedServer : DefaultServer
    {
        public CustomizedServer(PostgreSqlTransport transport)
        {
            TransportConfiguration = new ConfigureEndpointPostgreSqlTransport(transport);
        }

        public CustomizedServer(string connectionString, bool supportsPublishSubscribe = true, bool supportsDelayedDelivery = true)
        {
            var transport = new PostgreSqlTransport(connectionString, TransportTransactionMode.SendsAtomicWithReceive, supportsDelayedDelivery, supportsPublishSubscribe, true);

            TransportConfiguration = new ConfigureEndpointPostgreSqlTransport(transport);
        }
    }
}