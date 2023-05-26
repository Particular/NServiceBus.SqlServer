using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Transport.SQLServer;
using TestLogicApi;

class MessageDrivenSubscriber : Base, ITestBehavior
{
    protected override void Configure(
        PluginOptions args,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
        )
    {
        var x = transportConfig.EnableMessageDrivenPubSubCompatibilityMode();
        x.RegisterPublisher(typeof(MyEvent), args.ApplyUniqueRunPrefix(nameof(MessageDrivenPublisher)));
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}