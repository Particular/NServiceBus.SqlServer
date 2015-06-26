namespace NServiceBus.AcceptanceTests.BasicMessaging
{
    using System;
    using EndpointTemplates;
    using AcceptanceTesting;
    using MessageMutator;
    using NUnit.Framework;

    public class When_using_a_custom_message_id : NServiceBusAcceptanceTest
    {
        static string MessageId = "my_custom_message_id";
       
        [Test]
        public void Should_preserve_the_given_id_when_delivering_the_message()
        {
            var context = new Context();

            Scenario.Define(context)
                    .WithEndpoint<Endpoint>(b => b.Given(bus =>
                    {
                        var msg = new MyRequest();
                        bus.SetMessageHeader(msg, Headers.MessageId, MessageId);
                        bus.SendLocal(msg);
                    }))
                    .Done(c => c.GotRequest)
                    .Run();

            Assert.AreEqual( MessageId, context.MessageIdReceived, "Message ids should match" );
        }

        public class Context : ScenarioContext
        {
            public bool GotRequest { get; set; }

            public string MessageIdReceived { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class GetValueOfIncomingMessageId:IMutateIncomingTransportMessages,INeedInitialization
            {
                public Context Context { get; set; }

                public void MutateIncoming(TransportMessage transportMessage)
                {
                    Context.MessageIdReceived = transportMessage.Id;
                }

                public void Init()
                {
                    Configure.Component<GetValueOfIncomingMessageId>(DependencyLifecycle.InstancePerCall);
                }
            }

            public class MyResponseHandler : IHandleMessages<MyRequest>
            {
                public Context Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyRequest response)
                {
                    Context.GotRequest = true;
                }
            }
        }


        [Serializable]
        public class MyRequest : IMessage
        {
        }
    }
}
