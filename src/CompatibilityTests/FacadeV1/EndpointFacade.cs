namespace SqlServerV1
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using NServiceBus.Features;
    using NServiceBus.Unicast.Subscriptions.MessageDrivenSubscriptions;
    using TransportCompatibilityTests.Common;
    using TransportCompatibilityTests.Common.Messages;
    using TransportCompatibilityTests.Common.SqlServer;

    class EndpointFacade : MarshalByRefObject, IEndpointFacade
    {
        IBus bus;
        IStartableBus startableBus;
        MessageStore messageStore;
        CallbackResultStore callbackResultStore;
        SubscriptionStore subscriptionStore;

        public void Dispose()
        {
            startableBus.Dispose();
        }

        public void Bootstrap(EndpointDefinition endpointDefinition)
        {
            var customConfigFile = new AppConfigGenerator()
                .Generate(SqlServerConnectionStringBuilder.Build(), endpointDefinition.As<SqlServerEndpointDefinition>().Schema, endpointDefinition.As<SqlServerEndpointDefinition>().Mappings);

            //HINT: we need to generate custom app.config because v1 sqltransports does a direct read from ConfigurationManager
            using (AppConfig.Change(customConfigFile.FullName))
            {
                var configure = Configure.With();
                configure.DefaultBuilder();

                configure.DefineEndpointName(endpointDefinition.Name);
                Address.InitializeLocalAddress(endpointDefinition.Name);

                configure.DefiningMessagesAs(t => t.Namespace != null && t.Namespace.EndsWith(".Messages") && t != typeof(TestEvent));
                configure.DefiningEventsAs(t => t == typeof(TestEvent));

                configure.UseInMemoryTimeoutPersister();
                configure.InMemorySubscriptionStorage();
                configure.UseTransport<SqlServer>();

                var customConfiguration = new CustomConfiguration(endpointDefinition.As<SqlServerEndpointDefinition>().Mappings);
                configure.CustomConfigurationSource(customConfiguration);

                Feature.Enable<MessageDrivenSubscriptions>();

                configure.Configurer.ConfigureComponent<MessageStore>(DependencyLifecycle.SingleInstance);

                startableBus = configure.UnicastBus().CreateBus();
                bus = startableBus.Start(() => configure.ForInstallationOn<NServiceBus.Installation.Environments.Windows>().Install());

                messageStore = (MessageStore)configure.Builder.Build(typeof(MessageStore));
                subscriptionStore = new SubscriptionStore();
                callbackResultStore = new CallbackResultStore();

                configure.Builder.Build<MessageDrivenSubscriptionManager>().ClientSubscribed += (sender, args) => { subscriptionStore.Increment(); };
            }
        }

        public void SendCommand(Guid messageId)
        {
            bus.Send(new TestCommand {Id = messageId});
        }

        public void SendRequest(Guid requestId)
        {
            bus.Send(new TestRequest {RequestId = requestId});
        }

        public void PublishEvent(Guid eventId)
        {
            bus.Publish<TestEvent>(e => e.EventId = eventId);
        }

        public void SendAndCallbackForInt(int value)
        {
            Task.Run(async () =>
            {
                var res = await bus.Send(new TestIntCallback {Response = value}).Register();

                callbackResultStore.Add(res);
            });
        }

        public void SendAndCallbackForEnum(CallbackEnum value)
        {
            Task.Run(async () =>
            {
                var res = await bus.Send(new TestEnumCallback {CallbackEnum = value}).Register<CallbackEnum>();

                callbackResultStore.Add(res);
            });
        }

        public Guid[] ReceivedMessageIds => messageStore.GetAll();

        public Guid[] ReceivedResponseIds => messageStore.Get<TestResponse>();

        public Guid[] ReceivedEventIds => messageStore.Get<TestEvent>();

        public int[] ReceivedIntCallbacks => callbackResultStore.Get<int>();

        public CallbackEnum[] ReceivedEnumCallbacks => callbackResultStore.Get<CallbackEnum>();

        public int NumberOfSubscriptions => subscriptionStore.NumberOfSubscriptions;

    }
}