using System.Threading.Tasks;
using NServiceBus;
using TestLogicApi;

class MessageDrivenSubscriber : Base, ITestBehavior
{
    public MessageDrivenSubscriber() : base("Subscriber")
    {
    }

    protected override void Configure(
        PluginOptions args,
        EndpointConfiguration endpointConfig,
        TransportExtensions<SqlServerTransport> transportConfig,
        RoutingSettings<SqlServerTransport> routingConfig
        )
    {
        routingConfig.RegisterPublisher(typeof(MyEvent), "Publisher");
    }

    public class MyEventHandler : IHandleMessages<MyEvent>
    {
        public Task Handle(MyEvent message, IMessageHandlerContext context)
        {
            return Task.CompletedTask;
        }
    }
}