using System.Collections.Generic;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;

public class ConfigureSqlServerTransport : IConfigureTestExecution
{
    BusConfiguration busConfiguration;

    public Task Configure(BusConfiguration configuration, IDictionary<string, string> settings)
    {
        busConfiguration = configuration;
        configuration.UseTransport<SqlServerTransport>().ConnectionString(settings["Transport.ConnectionString"]);
        return Task.FromResult(0);
    }

    public Task Cleanup()
    {
        //TODO: add logic for removing queues used in tests

        return Task.FromResult(0);
    }
}
