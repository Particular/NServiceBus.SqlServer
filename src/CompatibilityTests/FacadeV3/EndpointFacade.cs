using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Pipeline;

using NServiceBus.SqlServer.CompatibilityTests.Common;
using NServiceBus.SqlServer.CompatibilityTests.Common.Messages;
using NServiceBus.Transport.SQLServer;

public class EndpointFacade : MarshalByRefObject, IEndpointFacade
{
    IEndpointInstance endpointInstance;
    MessageStore messageStore;
    CallbackResultStore callbackResultStore;
    SubscriptionStore subscriptionStore;

    async Task InitializeEndpoint(SqlServerEndpointDefinition endpointDefinition)
    {
        var endpointConfiguration = new EndpointConfiguration(endpointDefinition.Name);

        endpointConfiguration.Conventions().DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages") && t != typeof(TestEvent));
        endpointConfiguration.Conventions().DefiningEventsAs(t => t == typeof(TestEvent));

        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UsePersistence<InMemoryPersistence>();
        endpointConfiguration.UseTransport<SqlServerTransport>()
            .ConnectionString(SqlServerConnectionStringBuilder.Build());

        if (!string.IsNullOrWhiteSpace(endpointDefinition.Schema))
        {
            endpointConfiguration
                .UseTransport<SqlServerTransport>()
                .DefaultSchema(endpointDefinition.Schema);
        }

        foreach (var mm in endpointDefinition.Mappings)
        {
            if (string.IsNullOrEmpty(mm.Schema) == false)
            {
                endpointConfiguration.UseTransport<SqlServerTransport>().UseSchemaForQueue(mm.TransportAddress, mm.Schema);
            }
        }

        endpointConfiguration.CustomConfigurationSource(new CustomConfiguration(endpointDefinition.Mappings));
        endpointConfiguration.MakeInstanceUniquelyAddressable("A");

        messageStore = new MessageStore();
        callbackResultStore = new CallbackResultStore();
        subscriptionStore = new SubscriptionStore();

        endpointConfiguration.RegisterComponents(c => c.RegisterSingleton(messageStore));
        endpointConfiguration.RegisterComponents(c => c.RegisterSingleton(subscriptionStore));

        endpointConfiguration.Pipeline.Register<SubscriptionMonitoringBehavior.Registration>();

        endpointInstance = await Endpoint.Start(endpointConfiguration);
    }

    public void Bootstrap(EndpointDefinition endpointDefinition)
    {
        InitializeEndpoint(endpointDefinition.As<SqlServerEndpointDefinition>())
            .GetAwaiter()
            .GetResult();
    }

    public void SendCommand(Guid messageId)
    {
        endpointInstance.Send(new TestCommand { Id = messageId }).GetAwaiter().GetResult();
    }

    public void SendRequest(Guid requestId)
    {
        endpointInstance.Send(new TestRequest { RequestId = requestId }).GetAwaiter().GetResult();
    }

    public void PublishEvent(Guid eventId)
    {
        endpointInstance.Publish(new TestEvent { EventId = eventId }).GetAwaiter().GetResult();
    }

    public void SendAndCallbackForInt(int value)
    {
        Task.Run(async () =>
        {
            var result = await endpointInstance.Request<int>(new TestIntCallback { Response = value }, new SendOptions());

            callbackResultStore.Add(result);
        });
    }

    public void SendAndCallbackForEnum(CallbackEnum value)
    {
        Task.Run(async () =>
        {
            var result = await endpointInstance.Request<CallbackEnum>(new TestEnumCallback { CallbackEnum = value }, new SendOptions());

            callbackResultStore.Add(result);
        });
    }

    public Guid[] ReceivedMessageIds => messageStore.GetAll();

    public Guid[] ReceivedResponseIds => messageStore.Get<TestResponse>();

    public Guid[] ReceivedEventIds => messageStore.Get<TestEvent>();

    public int[] ReceivedIntCallbacks => callbackResultStore.Get<int>();

    public CallbackEnum[] ReceivedEnumCallbacks => callbackResultStore.Get<CallbackEnum>();

    public int NumberOfSubscriptions => subscriptionStore.NumberOfSubscriptions;

    public void Dispose()
    {
        endpointInstance.Stop().GetAwaiter().GetResult();
    }

    class SubscriptionMonitoringBehavior : Behavior<IIncomingPhysicalMessageContext>
    {
        public SubscriptionStore SubscriptionStore { get; set; }

        public override async Task Invoke(IIncomingPhysicalMessageContext context, Func<Task> next)
        {
            await next();
            string intent;

            if (context.Message.Headers.TryGetValue(Headers.MessageIntent, out intent) && intent == "Subscribe")
            {
                SubscriptionStore.Increment();
            }
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("SubscriptionBehavior", typeof(SubscriptionMonitoringBehavior), "So we can get subscription events")
            {
                InsertBefore("ProcessSubscriptionRequests");
            }
        }
    }
}
