namespace NServiceBus.AcceptanceTests.PubSub
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using EndpointTemplates;
    using AcceptanceTesting;
    using Features;
    using NUnit.Framework;
    using ScenarioDescriptors;
    using Unicast.Subscriptions;
    using Unicast.Subscriptions.MessageDrivenSubscriptions;

    public class When_publishing_an_event_with_the_subscriber_scaled_out : NServiceBusAcceptanceTest
    {
        static string Server1 = "Server1";
        static string Server2 = "Server2";

        [Test]//https://github.com/NServiceBus/NServiceBus/issues/1101
        public void Should_only_publish_one_event()
        {
            Scenario.Define<Context>()
                    .WithEndpoint<Publisher>(b =>
                        b.When(c => c.NumberOfSubscriptionsReceived >= 2, (bus, c) => bus.SendLocal(new ListSubscribers()))
                     )
                    .WithEndpoint<Subscriber1>(b => b.Given((bus, context) => bus.Subscribe<MyEvent>()))
                    .WithEndpoint<Subscriber2>(b => b.Given((bus, context) => bus.Subscribe<MyEvent>()))
                    .Done(c => c.Done)
                    .Repeat(r => r.For(Transports.Default))
                    .Should(c => Assert.AreEqual(1, c.SubscribersOfTheEvent.Count, "There should only be one logical subscriber"))
                    .Run();
        }

        public class Context : ScenarioContext
        {
            public int NumberOfSubscriptionsReceived { get; set; }
            public IList<string> SubscribersOfTheEvent { get; set; }
            public bool Done { get; set; }
        }

        public class Publisher : EndpointConfigurationBuilder
        {
            public Publisher()
            {
                EndpointSetup<DefaultPublisher>(b => b.OnEndpointSubscribed<Context>((args, context) =>
                {
                    if (args.SubscriberReturnAddress.Queue != "MyEndpoint")
                    {
                        return;
                    }

                    context.NumberOfSubscriptionsReceived++;
                }));
            }

            public class ListSubscribersHandler : IHandleMessages<ListSubscribers>
            {
                public ISubscriptionStorage SubscriptionStorage { get; set; }
                public Context Context { get; set; }

                public void Handle(ListSubscribers message)
                {
                    Context.SubscribersOfTheEvent = SubscriptionStorage
                                                              .GetSubscriberAddressesForMessage(new[] { new MessageType(typeof(MyEvent)) }).Select(a => a.ToString()).ToList();
                    Context.Done = true;
                }
            }
        }

        public class Subscriber1 : EndpointConfigurationBuilder
        {
            public Subscriber1()
            {
                EndpointSetup<DefaultServer>(c => c.DisableFeature<AutoSubscribe>())
                    .AddMapping<MyEvent>(typeof (Publisher))
                    .CustomMachineName(Server1)
                    .CustomEndpointName("MyEndpoint");
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public void Handle(MyEvent messageThatIsEnlisted)
                {
                }
            }
        }

        public class Subscriber2 : EndpointConfigurationBuilder
        {
            public Subscriber2()
            {
                EndpointSetup<DefaultServer>(c => c.DisableFeature<AutoSubscribe>())
                        .AddMapping<MyEvent>(typeof(Publisher))
                        .CustomMachineName(Server2)
                        .CustomEndpointName("MyEndpoint");
            }

            public class MyEventHandler : IHandleMessages<MyEvent>
            {
                public Context Context { get; set; }

                public void Handle(MyEvent messageThatIsEnlisted)
                {
                }
            }
        }

        [Serializable]
        public class MyEvent : IEvent
        {
        }

        [Serializable]
        public class ListSubscribers : ICommand
        {
        }
    }
}