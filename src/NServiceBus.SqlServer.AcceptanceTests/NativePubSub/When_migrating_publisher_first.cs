namespace NServiceBus.AcceptanceTests.NativePubSub
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using Features;
    using NServiceBus.Routing.MessageDrivenSubscriptions;
    using NUnit.Framework;
    using Transport.SQLServer;
    using Conventions = AcceptanceTesting.Customization.Conventions;

    public class When_migrating_publisher_first : NServiceBusAcceptanceTest
    {
        static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));

        [Test]
        public async Task Should_not_lose_any_events()
        {
            var subscriptionStorage = new TestingInMemorySubscriptionStorage();

            //Before migration begins
            var beforeMigration = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                        c.GetSettings().Set("SqlServer.DisableNativePubSub", true);
                    });
                    b.When(c => c.SubscribedMessageDriven, (session, ctx) => session.Publish(new MyEvent()));
                })

                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.GetSettings().Set("SqlServer.DisableNativePubSub", true);
                        c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig", new List<PublisherTableEntry>
                        {
                            new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                        });
                    });
                    b.When(async (session, ctx) =>
                    {
                        await session.Subscribe<MyEvent>();
                    });
                })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(30));

            Assert.True(beforeMigration.GotTheEvent);

            //Publisher migrated and in compatibility mode
            var publisherMigrated = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                        c.ConfigureSqlServerTransport().EnableMessageDrivenPubSubCompatibilityMode();
                    });
                    b.When(c => c.EndpointsStarted, (session, ctx) => session.Publish(new MyEvent()));
                })

                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.GetSettings().Set("SqlServer.DisableNativePubSub", true);
                        c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig", new List<PublisherTableEntry>
                        {
                            new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                        });
                    });
                    b.When(async (session, ctx) =>
                    {
                        await session.Subscribe<MyEvent>();
                    });
                })
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(30));

            Assert.True(publisherMigrated.GotTheEvent);

            //Subscriber migrated and in compatibility mode
            var subscriberMigratedRunSettings = new RunSettings
            {
                TestExecutionTimeout = TimeSpan.FromSeconds(30)
            };
            subscriberMigratedRunSettings.Set("DoNotCleanNativeSubscriptions", true);
            var subscriberMigrated = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                        c.ConfigureSqlServerTransport().EnableMessageDrivenPubSubCompatibilityMode();
                    });
                    b.When(c => c.SubscribedMessageDriven && c.SubscribedNative, (session, ctx) => session.Publish(new MyEvent()));
                })

                .WithEndpoint<Subscriber>(b =>
                {
                    b.CustomConfig(c =>
                    {
                        var compatModeSettings = c.ConfigureSqlServerTransport().EnableMessageDrivenPubSubCompatibilityMode();
                        compatModeSettings.RegisterPublisher(typeof(MyEvent), PublisherEndpoint);
                    });
                    b.When(async (session, ctx) =>
                    {
                        //Subscribes both using native feature and message-driven
                        await session.Subscribe<MyEvent>();
                        ctx.SubscribedNative = true;
                    });
                })
                .Done(c => c.GotTheEvent)
                .Run(subscriberMigratedRunSettings);

            Assert.True(subscriberMigrated.GotTheEvent);

            //Compatibility mode disabled in both publisher and subscriber
            var compatModeDisabled = await Scenario.Define<Context>()
                .WithEndpoint<Publisher>(b =>
                {
                    b.When(c => c.EndpointsStarted, (session, ctx) => session.Publish(new MyEvent()));
                })
                .WithEndpoint<Subscriber>()
                .Done(c => c.GotTheEvent)
                .Run(TimeSpan.FromSeconds(30));

            Assert.True(compatModeDisabled.GotTheEvent);
        }

        public class Context : ScenarioContext
        {
            public bool GotTheEvent { get; set; }
            public bool SubscribedMessageDriven { get; set; }
            public bool SubscribedNative { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(c =>
                {
                    c.OnEndpointSubscribed<Context>((s, context) =>
                    {
                        if (s.SubscriberEndpoint.Contains(Conventions.EndpointNamingConvention(typeof(Subscriber))))
                        {
                            context.SubscribedMessageDriven = true;
                        }
                    });
                }).IncludeType<TestingInMemorySubscriptionPersistence>();
            }
        }

        public class Subscriber : EndpointConfigurationBuilder
        {
            public Subscriber()
            {
                EndpointSetup<DefaultServer>(c =>
                    {
                        c.DisableFeature<AutoSubscribe>();
                    },
                    metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public Task Handle(MyEvent @event, IMessageHandlerContext context)
                {
                    Context.GotTheEvent = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyEvent : IEvent
        {
        }
    }
}