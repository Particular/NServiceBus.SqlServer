namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Pipeline;

    // Verifies that we don't accidentally break interop with persisters that participate in the transport transaction.
    public class When_message_handling_pipeline_is_invoked : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_provide_connection_and_transaction_via_transport_transaction()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<AnEndpoint>(c =>
                {
                    c.DoNotFailOnErrorMessages();
                    c.When(async bus =>
                    {
                        await bus.SendLocal(new InitiatingMessage());
                    });
                })
                .Done(c => c.TransportTransaction != null)
                .Run(TimeSpan.FromSeconds(10));

            Assert.IsNotNull(context.TransportTransaction);
            var transportTransaction = context.TransportTransaction;

            Assert.IsTrue(transportTransaction.TryGet("System.Data.SqlClient.SqlConnection", out object connection));
            Assert.IsInstanceOf<DbConnection>(connection);

            Assert.IsTrue(transportTransaction.TryGet("System.Data.SqlClient.SqlTransaction", out object transaction));
            Assert.IsInstanceOf<DbTransaction>(transaction);
        }

        class InitiatingMessage : IMessage
        {
        }

        class ParticipatingInTransportTransactionBehavior : IBehavior<IIncomingPhysicalMessageContext, IIncomingPhysicalMessageContext>
        {
            readonly MyContext testContext;

            public ParticipatingInTransportTransactionBehavior(MyContext testContext)
            {
                this.testContext = testContext;
            }
            public Task Invoke(IIncomingPhysicalMessageContext context, Func<IIncomingPhysicalMessageContext, Task> next)
            {
                var transaction = context.Extensions.Get<TransportTransaction>();
                testContext.TransportTransaction = transaction;

                return Task.CompletedTask;
            }
        }

        class MyContext : ScenarioContext
        {
            public TransportTransaction TransportTransaction { get; set; }
        }

        class AnEndpoint : EndpointConfigurationBuilder
        {
            public AnEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.LimitMessageProcessingConcurrencyTo(1);
                    c.Pipeline.Register(typeof(ParticipatingInTransportTransactionBehavior), "Behavior interested in the transport transaction");
                    c.ConfigureSqlServerTransport().TransportTransactionMode = TransportTransactionMode.SendsAtomicWithReceive;
                });
            }
        }
    }
}