using System.Threading.Tasks;
using System.Threading;
using NServiceBus;
using System.Linq;

abstract class Base
{
    readonly string endpointName;

    protected Base(string endpointName)
    {
        this.endpointName = endpointName;
    }

    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var config = new EndpointConfiguration(endpointName);
        config.EnableInstallers();
        config.PurgeOnStartup(true);

        var transport = new SqlServerTransport(opts.ConnectionString)
        {
            TransportTransactionMode = TransportTransactionMode.ReceiveOnly
        };

        config.Conventions().DefiningMessagesAs(t => t.GetInterfaces().Any(x => x.Name == "IMessage"));
        config.Conventions().DefiningCommandsAs(t => t.GetInterfaces().Any(x => x.Name == "ICommand"));
        config.Conventions().DefiningEventsAs(t => t.GetInterfaces().Any(x => x.Name == "IEvent"));

        var routingConfig = config.UseTransport(transport);
        config.AuditProcessedMessagesTo(opts.AuditQueue);
        config.AddHeaderToAllOutgoingMessages(nameof(opts.TestRunId), opts.TestRunId);
        config.Pipeline.Register(new DiscardBehavior(opts.TestRunId), nameof(DiscardBehavior));

        Configure(opts, config, transport, routingConfig);

        return config;
    }

    protected virtual void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        SqlServerTransport transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
        )
    {
    }

    public virtual Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}