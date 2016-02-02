namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Threading.Tasks;

    /*
    public class When_using_native_transactions : NServiceBusAcceptanceTest
    {
        [Test]
        public async void Should_pass_transaction_to_persistnace_storage()
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When(s => s.SendLocal(new SagaMessage())))
                .Run();
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServerTransport>().Transactions(TransportTransactionMode.SendsAtomicWithReceive);
                    c.UsePersistence<NHibernatePersistence>();
                });
            }

            public class TestSaga : Saga<SagaData>, IAmStartedByMessages<SagaMessage>
            {
                public Context TestContext { get; set; }

                public Task Handle(SagaMessage message, IMessageHandlerContext context)
                {
                    TestContext.SagaValue = Data.Counter++;

                    return Task.FromResult(0);
                }

                protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
                {
                    mapper.ConfigureMapping<SagaMessage>(m => m.SomeId)
                          .ToSaga(s => s.SomeId);
                }
            }

            public class SagaData : ContainSagaData
            {
                public virtual Guid SomeId { get; set; }
                public virtual int Counter { get; set; }
            }

        }

        class SagaMessage : IMessage
        {
            public Guid SomeId { get; set; }
        }

        class Context : ScenarioContext
        {
            public int SagaValue { get; set; }
        }
    }
    */
}