namespace NServiceBus.Transport.SqlServer.AcceptanceTests.NativePubSub;

using System;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Configuration.AdvancedExtensibility;
using Features;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;
using Routing.MessageDrivenSubscriptions;

public class When_publisher_runs_in_compat_mode : NServiceBusAcceptanceTest
{
    static string PublisherEndpoint => Conventions.EndpointNamingConvention(typeof(MigratedPublisher));
    static readonly string ConnectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

    [Test]
    public async Task Legacy_subscriber_can_subscribe()
    {
        var publisherMigrated = await Scenario.Define<Context>()
            .WithEndpoint<MigratedPublisher>(b => b.When(c => c.SubscribedMessageDriven, (session, ctx) => session.Publish(new MyEvent())))
            .WithEndpoint<Subscriber>(b => b.When((session, ctx) => session.Subscribe<MyEvent>()))
            .Run();

        Assert.That(publisherMigrated.GotTheEvent, Is.True);
    }

    public class Context : ScenarioContext
    {
        public bool GotTheEvent { get; set; }
        public bool SubscribedMessageDriven { get; set; }
    }

    public class MigratedPublisher : EndpointConfigurationBuilder
    {
        public MigratedPublisher() =>
            EndpointSetup<DefaultPublisher>(c =>
            {
#pragma warning disable CS0618 // Type or member is obsolete
                // When message-driven compatibility mode is obsoleted with an error this test can be removed
                c.ConfigureRouting().EnableMessageDrivenPubSubCompatibilityMode();
#pragma warning restore CS0618 // Type or member is obsolete
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
        public Subscriber() =>
            EndpointSetup(new CustomizedServer(ConnectionString, false), (c, sd) =>
            {
                //SqlServerTransport no longer implements message-driven pub sub interface so we need to configure Publishers "manually"
                c.GetSettings().GetOrCreate<Publishers>().AddOrReplacePublishers("LegacyConfig",
                [
                    new PublisherTableEntry(typeof(MyEvent), PublisherAddress.CreateFromEndpointName(PublisherEndpoint))
                ]);
                c.DisableFeature<AutoSubscribe>();
            });

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
