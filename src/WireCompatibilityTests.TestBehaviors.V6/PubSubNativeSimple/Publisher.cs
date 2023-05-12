using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Publisher : ITestBehavior, IPublisher
{
    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args[Keys.ConnectionString];

        var config = new EndpointConfiguration("Publisher");
        config.EnableInstallers();

        var transport = config.UseTransport<SqlServerTransport>()
            .ConnectionString(connectionString)
            .Transactions(TransportTransactionMode.ReceiveOnly);

        config.AuditProcessedMessagesTo(Keys.AuditQueue);
        config.AddHeaderToAllOutgoingMessages(Keys.TestRunId, args[Keys.TestRunId]);
        config.Pipeline.Register(new DiscardBehavior(args[Keys.TestRunId]), nameof(DiscardBehavior));

        Configure(args, config, transport);

        return config;
    }

    protected virtual void Configure(
        Dictionary<string, string> args,
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
#pragma warning disable PS0018
        public Task Handle(MyEvent message, IMessageHandlerContext context)
#pragma warning restore PS0018
        {
            return Task.CompletedTask;
        }
    }
}
