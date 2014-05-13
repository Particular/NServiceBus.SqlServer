namespace NServiceBus.AcceptanceTests.BasicMessaging
{
    using System;
    using System.Transactions;
    using AcceptanceTesting;
    using EndpointTemplates;
    using NUnit.Framework;

    public class When_sharing_the_receive_connection : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_reuse_it_for_sends_to_the_same_db()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<ConnectionSharingEndpoint>(b => b.Given(bus => bus.SendLocal(new StartMessage())))
                .Done(c => c.Done)
                .Run();

            Assert.False(context.IsDtcTransaction);
        }

     
        public class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public bool IsDtcTransaction { get; set; }
        }

        [Serializable]
        public class StartMessage : IMessage { }

        [Serializable]
        public class AnotherMessage : IMessage { }

        public class ConnectionSharingEndpoint : EndpointConfigurationBuilder
        {
            public ConnectionSharingEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

          
            class FirstHandler : IHandleMessages<StartMessage>
            {
                public Context Context { get; set; }
                public IBus Bus { get; set; }

                public void Handle(StartMessage message)
                {
                    Bus.SendLocal(new AnotherMessage());

                    Context.IsDtcTransaction = Transaction.Current.TransactionInformation.DistributedIdentifier != Guid.Empty;
                }
            }

            class SecondHandler : IHandleMessages<AnotherMessage>
            {
                public Context Context { get; set; }
                public IBus Bus { get; set; }

                public void Handle(AnotherMessage message)
                {
                    Context.Done = true;
                }
            }
        }
    }
}