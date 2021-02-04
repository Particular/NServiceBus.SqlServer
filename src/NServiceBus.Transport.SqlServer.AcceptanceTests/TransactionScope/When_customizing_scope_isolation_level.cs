namespace NServiceBus.Transport.SqlServer.AcceptanceTests.TransactionScope
{
    using System.Threading.Tasks;
    using System.Transactions;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_customizing_scope_isolation_level : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_honor_configured_level()
        {
            Requires.DtcSupport();

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(c => c.When(b => b.SendLocal(new MyMessage())))
                .Done(c => c.Done)
                .Run();

            Assert.True(context.AmbientTransactionPresent, "There should be an ambient transaction present");
            Assert.AreEqual(IsolationLevel.RepeatableRead, context.IsolationLevel, "Ambient transaction should have configured isolation level");
        }

        public class MyMessage : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public bool AmbientTransactionPresent { get; set; }
            public IsolationLevel IsolationLevel { get; set; }
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.ConfigureSqlServerTransport();
                    transport.TransportTransactionMode = TransportTransactionMode.TransactionScope;
                    transport.TransactionScope.IsolationLevel = IsolationLevel.RepeatableRead;
                });
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context scenarioContext;
                public MyMessageHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var ambientTransactionPresent = Transaction.Current != null;

                    scenarioContext.AmbientTransactionPresent = ambientTransactionPresent;
                    if (ambientTransactionPresent)
                    {
                        scenarioContext.IsolationLevel = Transaction.Current.IsolationLevel;
                    }
                    scenarioContext.Done = true;

                    return Task.FromResult(0);
                }
            }
        }
    }
}