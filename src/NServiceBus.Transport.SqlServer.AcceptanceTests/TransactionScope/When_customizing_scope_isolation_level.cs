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
                EndpointSetup<DefaultServer>(busConfiguration =>
                {
                    busConfiguration.UseTransport<SqlServerTransport>()
                        .Transactions(TransportTransactionMode.TransactionScope)
                        .TransactionScopeOptions(isolationLevel: IsolationLevel.RepeatableRead);
                });
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    var ambientTransactionPresent = Transaction.Current != null;

                    Context.AmbientTransactionPresent = ambientTransactionPresent;
                    if (ambientTransactionPresent)
                    {
                        Context.IsolationLevel = Transaction.Current.IsolationLevel;
                    }
                    Context.Done = true;

                    return Task.FromResult(0);
                }
            }
        }
    }
}