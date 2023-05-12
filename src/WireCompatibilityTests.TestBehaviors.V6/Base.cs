using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using NServiceBus;

abstract class Base
{
    readonly string endpointName;

    protected Base(string endpointName)
    {
        this.endpointName = endpointName;
    }

    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args[Keys.ConnectionString];

        var config = new EndpointConfiguration(endpointName);
        config.EnableInstallers();
        config.PurgeOnStartup(true);

        TransportExtensions<SqlServerTransport> transport = config.UseTransport<SqlServerTransport>()
            .ConnectionString(connectionString)
            .Transactions(TransportTransactionMode.ReceiveOnly);

        config.AuditProcessedMessagesTo(Keys.AuditQueue);
        config.AddHeaderToAllOutgoingMessages(Keys.TestRunId, args[Keys.TestRunId]);
        config.Pipeline.Register(new DiscardBehavior(args[Keys.TestRunId]), nameof(DiscardBehavior));

        Configure(args, config, transport);

        return config;
    }

    public virtual void Configure(
        Dictionary<string, string> args,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig
        )
    { }

    public virtual Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}