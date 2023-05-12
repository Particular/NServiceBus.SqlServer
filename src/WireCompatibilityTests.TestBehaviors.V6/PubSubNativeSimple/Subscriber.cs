using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class Subscriber : ITestBehavior, ISubscriber
{
    public EndpointConfiguration Configure(Dictionary<string, string> args)
    {
        var connectionString = args[Keys.ConnectionString];

        var config = new EndpointConfiguration("Subscriber");
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

    public Task Execute(IEndpointInstance endpointInstance, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
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
