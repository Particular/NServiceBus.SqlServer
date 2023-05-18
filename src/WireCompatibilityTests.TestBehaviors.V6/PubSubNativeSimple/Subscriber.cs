using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Subscriber : ITestBehavior, ISubscriber
{
    public EndpointConfiguration Configure(PluginOptions opts)
    {
        var config = new EndpointConfiguration(opts.ApplyUniqueRunPrefix("Subscriber"));
        config.EnableInstallers();

        var transport = config.UseTransport<SqlServerTransport>()
            .ConnectionString(opts.ConnectionString)
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

    public Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}
