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

    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var config = new EndpointConfiguration(opts.ApplyUniqueRunPrefix(endpointName));
        config.EnableInstallers();
        config.PurgeOnStartup(true);

        TransportExtensions<SqlServerTransport> transport = config.UseTransport<SqlServerTransport>()
            .ConnectionString(opts.ConnectionString + $";App={endpointName}")
            .Transactions(TransportTransactionMode.ReceiveOnly);

        transport.SubscriptionSettings().SubscriptionTableName(opts.ApplyUniqueRunPrefix("SubscriptionRouting"));

        config.SendFailedMessagesTo(opts.ApplyUniqueRunPrefix("error"));
        config.AuditProcessedMessagesTo(opts.AuditQueue);
        config.AddHeaderToAllOutgoingMessages(nameof(opts.TestRunId), opts.TestRunId);
        config.Pipeline.Register(new DiscardBehavior(opts.TestRunId), nameof(DiscardBehavior));

        Configure(opts, config, transport);

        return config;
    }

    public virtual void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig
        )
    { }

    public virtual Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}