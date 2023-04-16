using NServiceBus;

public class SetupTransport
{
    public void Configure(EndpointConfiguration endpointConfiguration)
    {
        var connectionString = Environment.GetEnvironmentVariable("SQLTESTSCONNECTIONSTRING");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Environment variable SQLTESTSCONNECTIONSTRING not set");
        }

        var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
    }
}
