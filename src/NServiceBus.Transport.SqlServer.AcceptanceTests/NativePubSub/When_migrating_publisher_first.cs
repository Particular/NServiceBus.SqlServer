namespace NServiceBus.Transport.SqlServer.AcceptanceTests.NativePubSub;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Support;
using Configuration.AdvancedExtensibility;
using Features;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Routing.MessageDrivenSubscriptions;
using Conventions = AcceptanceTesting.Customization.Conventions;

public class When_migrating_publisher_first : NServiceBusAcceptanceTest
{
    static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(Publisher));
    static readonly string _connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

    [Test]
    public async Task Should_not_lose_any_events()
    {
        var subscriptionStorage = new TestingInMemorySubscriptionStorage();

        //Before migration begins
        var beforeMigration = await Scenario.Define<Context>()
            .WithEndpoint(new Publisher(_connectionString, false), b =>
            {
                b.CustomConfig(c =>
                {
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                });
                b.When(c => c.SubscribedMessageDriven, (session, ctx) => session.Publish(new MyEvent()));
            })
            .WithEndpoint(new Subscriber(_connectionString, false), b =>
            {
                b.CustomConfig(c =>
                {
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                    [
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                    ]);
                });
                b.When(async (session, _) =>
                {
                    await session.Subscribe<MyEvent>();
                });
            })
            .Run();

        Assert.That(beforeMigration.GotTheEvent, Is.True);

        //Publisher migrated and in compatibility mode
        var publisherMigrated = await Scenario.Define<Context>()
            .WithEndpoint(new Publisher(_connectionString, true), b =>
            {
                b.CustomConfig(c =>
                {
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                });
                b.When((session, ctx) => session.Publish(new MyEvent()));
            })
            .WithEndpoint(new Subscriber(_connectionString, false), b =>
            {
                b.CustomConfig(c =>
                {
                    c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                    [
                        new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                    ]);
                });
                b.When(async (session, _) =>
                {
                    await session.Subscribe<MyEvent>();
                });
            })
            .Run();

        Assert.That(publisherMigrated.GotTheEvent, Is.True);

        //Subscriber migrated and in compatibility mode
        var subscriberMigratedRunSettings = new RunSettings();
        subscriberMigratedRunSettings.Set("DoNotCleanNativeSubscriptions", true);
        var subscriberMigrated = await Scenario.Define<Context>()
            .WithEndpoint(new Publisher(_connectionString, true), b =>
            {
                b.CustomConfig(c =>
                {
                    c.UsePersistence<TestingInMemoryPersistence, StorageType.Subscriptions>().UseStorage(subscriptionStorage);
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                });
                b.When(c => c is { SubscribedMessageDriven: true, SubscribedNative: true }, (session, _) => session.Publish(new MyEvent()));
            })
            .WithEndpoint(new Subscriber(_connectionString, true), b =>
            {
                b.CustomConfig(c =>
                {
                    c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
                    var compatModeSettings = new SubscriptionMigrationModeSettings(c.GetSettings());
                    compatModeSettings.RegisterPublisher(typeof(MyEvent), PublisherEndpoint);
                });
                b.When(async (session, ctx) =>
                {
                    //Subscribes both using native feature and message-driven
                    await session.Subscribe<MyEvent>();
                    ctx.SubscribedNative = true;
                });
            })
            .Run(subscriberMigratedRunSettings);

        Assert.That(subscriberMigrated.GotTheEvent, Is.True);

        //Compatibility mode disabled in both publisher and subscriber
        var compatModeDisabled = await Scenario.Define<Context>()
            .WithEndpoint<Publisher>(b =>
            {
                b.When((session, ctx) => session.Publish(new MyEvent()));
            })
            .WithEndpoint<Subscriber>()
            .Run();

        Assert.That(compatModeDisabled.GotTheEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool GotTheEvent { get; set; }
        public bool SubscribedMessageDriven { get; set; }
        public bool SubscribedNative { get; set; }
    }

    public class Publisher : EndpointConfigurationBuilder
    {
        public Publisher() : this(_connectionString, true)
        {

        }

        public Publisher(string connectionString, bool supportsPublishSubscribe) =>
            EndpointSetup(new CustomizedServer(_connectionString, supportsPublishSubscribe), (c, rd) =>
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

    public class Subscriber : EndpointConfigurationBuilder
    {
        public Subscriber() : this(_connectionString, true)
        {
        }

        public Subscriber(string connectionString, bool supportsPublishSubscribe) =>
            EndpointSetup(new CustomizedServer(connectionString, supportsPublishSubscribe), (c, rd) =>
                {
                    c.DisableFeature<AutoSubscribe>();
                },
                metadata => metadata.RegisterPublisherFor<MyEvent>(typeof(Publisher)));

        public class Handler(Context scenarioContext) : IHandleMessages<MyEvent>
        {
            public Task Handle(MyEvent @event, IMessageHandlerContext context)
            {
                scenarioContext.GotTheEvent = true;
                scenarioContext.MarkAsCompleted();
                return Task.CompletedTask;
            }
        }
    }

    public class MyEvent : IEvent;
}