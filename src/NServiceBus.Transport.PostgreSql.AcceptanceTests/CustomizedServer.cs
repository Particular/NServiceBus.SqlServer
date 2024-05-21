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
    }
}