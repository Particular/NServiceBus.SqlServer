namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    public class When_processing_messages : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_allow_injecting_the_storage_context()
        {
            var context = new Context();

            Scenario.Define(context)
                .WithEndpoint<Endpoint>(b => b.Given(bus => bus.SendLocal(new TestMessage())))
                .Done(c => c.Done)
                .Run();

            Assert.True(context.ContextInjected);
        }

        public class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public bool ContextInjected { get; set; }
        }


        [Serializable]
        public class TestMessage : IMessage { }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(b => b.Transactions().DisableDistributedTransactions());
            }

            class Handler : IHandleMessages<TestMessage>
            {
                public Context Context { get; set; }
                public SqlServerStorageContext StorageContext { get; set; }

                public void Handle(TestMessage message)
                {
                    Context.ContextInjected = StorageContext != null && StorageContext.Connection != null && StorageContext.Transaction != null;
                    Context.Done = true;
                }
            }
        }

    }
}