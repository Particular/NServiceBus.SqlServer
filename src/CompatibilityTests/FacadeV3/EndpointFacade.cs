using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using CompatibilityTests.Common;
using CompatibilityTests.Common.Messages;
using NServiceBus;
using NServiceBus.Config;
using NServiceBus.Pipeline;
using NServiceBus.Support;
using NServiceBus.Transport.SQLServer;

public class EndpointFacade : MarshalByRefObject, IEndpointFacade, IEndpointConfigurationV3
{
    IEndpointInstance endpointInstance;
    MessageStore messageStore;
    CallbackResultStore callbackResultStore;
    SubscriptionStore subscriptionStore;
    EndpointConfiguration endpointConfiguration;
    string customConnectionString;
    CustomConfiguration customConfiguration;
    bool enableCallbacks;
    string instanceId;

    public IEndpointConfiguration Bootstrap(EndpointDefinition endpointDefinition)
    {
        if (endpointDefinition.MachineName != null)
        {
            RuntimeEnvironment.MachineNameAction = () => endpointDefinition.MachineName;
        }

        endpointConfiguration = new EndpointConfiguration(endpointDefinition.Name);

        endpointConfiguration.Conventions().DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages") && t != typeof(TestEvent));
        endpointConfiguration.Conventions().DefiningEventsAs(t => t == typeof(TestEvent));

        endpointConfiguration.EnableInstallers();
        endpointConfiguration.UsePersistence<InMemoryPersistence>();

        customConfiguration = new CustomConfiguration();
        endpointConfiguration.CustomConfigurationSource(customConfiguration);

        endpointConfiguration.Recoverability().Immediate(i => i.NumberOfRetries(0));
        endpointConfiguration.Recoverability().Delayed(d => d.NumberOfRetries(0));

        endpointConfiguration.AuditProcessedMessagesTo("audit");

        messageStore = new MessageStore();
        callbackResultStore = new CallbackResultStore();
        subscriptionStore = new SubscriptionStore();

        endpointConfiguration.RegisterComponents(c => c.RegisterSingleton(messageStore));
        endpointConfiguration.RegisterComponents(c => c.RegisterSingleton(subscriptionStore));

        endpointConfiguration.Pipeline.Register<SubscriptionMonitoringBehavior.Registration>();

        return this;
    }

    public void UseConnectionString(string connectionString)
    {
        customConnectionString = connectionString;
    }

    public void MapMessageToEndpoint(Type messageType, string destination)
    {
        customConfiguration.AddMapping(new MessageEndpointMapping
        {
            Endpoint = destination,
            Messages = messageType.AssemblyQualifiedName
        });
        endpointConfiguration.UseTransport<SqlServerTransport>().Routing().RouteToEndpoint(messageType, destination);
    }

    public void Start()
    {
        endpointConfiguration.UseTransport<SqlServerTransport>()
            .ConnectionString(customConnectionString ?? SqlServerConnectionStringBuilder.Build());

        if (!enableCallbacks)
        {
            var callbackFeature = typeof(RequestResponseExtensions).Assembly.GetType("NServiceBus.Features.CallbackSupport", true);
            endpointConfiguration.DisableFeature(callbackFeature);
        }
        else
        {
            endpointConfiguration.MakeInstanceUniquelyAddressable(instanceId);
        }

        StartAsync().GetAwaiter().GetResult();
    }

    async Task StartAsync()
    {
        endpointInstance = await Endpoint.Start(endpointConfiguration);
    }

    public void EnableCallbacks(string instanceId)
    {
        enableCallbacks = true;
        this.instanceId = instanceId;
    }

    public void DefaultSchema(string schema)
    {
        endpointConfiguration.UseTransport<SqlServerTransport>().DefaultSchema(schema);
    }

    public void UseSchemaForQueue(string queue, string schema)
    {
        endpointConfiguration.UseTransport<SqlServerTransport>().UseSchemaForQueue(queue, schema);
    }

    public void UseLagacyMultiInstanceMode(Dictionary<string, string> connectionStringMap)
    {
#pragma warning disable 0618
        endpointConfiguration.UseTransport<SqlServerTransport>().EnableLegacyMultiInstanceMode(async address =>
        {
            var connectionString = connectionStringMap.FirstOrDefault(x => address.StartsWith(x.Key));
            var connection = new SqlConnection(connectionString.Value);
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        });
#pragma warning restore 0618
    }

    public void UseSchemaForEndpoint(string endpoint, string schema)
    {
        endpointConfiguration.UseTransport<SqlServerTransport>().UseSchemaForEndpoint(endpoint, schema);
    }

    public void RouteToEndpoint(Type messageType, string endpoint)
    {
        endpointConfiguration.UseTransport<SqlServerTransport>().Routing().RouteToEndpoint(messageType, endpoint);
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
