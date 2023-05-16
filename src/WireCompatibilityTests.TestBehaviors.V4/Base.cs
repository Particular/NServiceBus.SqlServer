using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;

class Base
{
    string endpointName;

    protected Base(string endpointName)
    {
        this.endpointName = endpointName;
    }

    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args["ConnectionString"];

        var config = new EndpointConfiguration(endpointName);
        config.EnableInstallers();
        config.UsePersistence<InMemoryPersistence>();

        var transport = config.UseTransport<SqlServerTransport>();
        transport.ConnectionString(connectionString);
        transport.Transactions(TransportTransactionMode.ReceiveOnly);

        config.AuditProcessedMessagesTo("AuditSpy");
        config.AddHeaderToAllOutgoingMessages(Keys.TestRunId, args[Keys.TestRunId]);
        config.Pipeline.Register(new DiscardBehavior(args[Keys.TestRunId]), nameof(DiscardBehavior));

        Configure(args, config, transport, transport.Routing());

        return config;
    }

    protected virtual void Configure(
        Dictionary<string, string> args,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
    )
    {
    }

    public virtual Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}