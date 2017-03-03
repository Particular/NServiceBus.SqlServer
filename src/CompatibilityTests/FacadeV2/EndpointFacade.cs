using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CompatibilityTests.Common;
using CompatibilityTests.Common.Messages;
using NServiceBus;
using NServiceBus.Pipeline;
using NServiceBus.Pipeline.Contexts;
using NServiceBus.Transports.SQLServer;

public class EndpointFacade : MarshalByRefObject, IEndpointFacade
{
    IBus bus;
    MessageStore messageStore;
    CallbackResultStore callbackResultStore;
    SubscriptionStore subscriptionStore;

    public void Bootstrap(EndpointDefinition endpointDefinition)
    {
        var busConfiguration = new BusConfiguration();

        busConfiguration.Conventions()
            .DefiningMessagesAs(
                t => t.Namespace != null && t.Namespace.EndsWith(".Messages") && t != typeof(TestEvent));
        busConfiguration.Conventions().DefiningEventsAs(t => t == typeof(TestEvent));

        busConfiguration.EndpointName(endpointDefinition.Name);
        busConfiguration.UsePersistence<InMemoryPersistence>();
        busConfiguration.EnableInstallers();
        var transport = busConfiguration.UseTransport<SqlServerTransport>();
        transport.ConnectionString(SqlServerConnectionStringBuilder.Build());

        if (!String.IsNullOrWhiteSpace(endpointDefinition.As<SqlServerEndpointDefinition>().Schema))
        {
            transport
                .DefaultSchema(endpointDefinition.As<SqlServerEndpointDefinition>().Schema);
        }

        foreach (var mapping in endpointDefinition.As<SqlServerEndpointDefinition>().Mappings)
        {
            var endpointConnections = new List<EndpointConnectionInfo>();

            if (mapping.Schema != null)
            {
                var endpointConnectionInfo = EndpointConnectionInfo
                    .For(mapping.TransportAddress)
                    .UseSchema(mapping.Schema);
                endpointConnections.Add(endpointConnectionInfo);
            }

            if (endpointConnections.Any())
            {
                transport
                    .UseSpecificConnectionInformation(endpointConnections);
            }
        }

        var customConfiguration = new CustomConfiguration(endpointDefinition.As<SqlServerEndpointDefinition>().Mappings);
        busConfiguration.CustomConfigurationSource(customConfiguration);

        messageStore = new MessageStore();
        subscriptionStore = new SubscriptionStore();
        callbackResultStore = new CallbackResultStore();

        busConfiguration.RegisterComponents(c => c.RegisterSingleton(messageStore));
        busConfiguration.RegisterComponents(c => c.RegisterSingleton(subscriptionStore));

        busConfiguration.Pipeline.Register<SubscriptionBehavior.Registration>();

        var startableBus = Bus.Create(busConfiguration);

        bus = startableBus.Start();
    }

    public void SendCommand(Guid messageId)
    {
        bus.Send(new TestCommand { Id = messageId });
    }

    public void SendRequest(Guid requestId)
    {
        bus.Send(new TestRequest { RequestId = requestId });
    }

    public void PublishEvent(Guid eventId)
    {
        bus.Publish(new TestEvent { EventId = eventId });
    }

    public void SendAndCallbackForEnum(CallbackEnum value)
    {
        Task.Run(async () =>
        {
            var res = await bus.Send(new TestEnumCallback { CallbackEnum = value }).Register<CallbackEnum>();

            callbackResultStore.Add(res);
        });
    }

    public void SendAndCallbackForInt(int value)
    {
        Task.Run(async () =>
        {
            var res = await bus.Send(new TestIntCallback { Response = value }).Register();

            callbackResultStore.Add(res);
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
        bus.Dispose();
    }

    class SubscriptionBehavior : IBehavior<IncomingContext>
    {
        public SubscriptionStore SubscriptionStore { get; set; }

        public void Invoke(IncomingContext context, Action next)
        {
            next();

            string intent;

            if (context.PhysicalMessage.Headers.TryGetValue(Headers.MessageIntent, out intent) && intent == "Subscribe")
            {
                SubscriptionStore.Increment();
            }
        }

        internal class Registration : RegisterStep
        {
            public Registration()
                : base("SubscriptionBehavior", typeof(SubscriptionBehavior), "So we can get subscription events")
            {
                InsertBefore(WellKnownStep.CreateChildContainer);
            }
        }
    }
}
