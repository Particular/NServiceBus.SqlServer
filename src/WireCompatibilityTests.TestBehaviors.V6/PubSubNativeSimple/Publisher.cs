using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Publisher : ITestBehavior, IPublisher
{
    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var connectionString = opts.ConnectionString;

        var config = new EndpointConfiguration("Publisher");
        config.EnableInstallers();

        var transport = config.UseTransport<SqlServerTransport>()
            .ConnectionString(connectionString)
            .Transactions(TransportTransactionMode.ReceiveOnly);

        config.AuditProcessedMessagesTo(opts.AuditQueue);
        config.AddHeaderToAllOutgoingMessages(nameof(opts.TestRunId), opts.TestRunId);
        config.Pipeline.Register(new DiscardBehavior(opts.TestRunId), nameof(DiscardBehavior));

        Configure(opts, config, transport);

        return config;
    }

    protected virtual void Configure(
        PluginOptions opts,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig
        )
    {
    }

    public async Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        await endpointInstance.Publish(new MyEvent()).ConfigureAwait(false);
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
