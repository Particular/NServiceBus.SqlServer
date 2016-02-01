namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Threading.Tasks;
    using System.Transactions;
    using Pipeline;
    using Persistence;
    using NUnit.Framework;

    public class When_using_native_transactions
    {
        const string ConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True";
        IEndpointInstance endpoint;
        Context context;

        [SetUp]
        public void SetUp()
        {
            var configuration = new BusConfiguration();
            configuration.SendFailedMessagesTo("error");
            configuration.PurgeOnStartup(true);
            configuration.LimitMessageProcessingConcurrencyTo(1);

            configuration.UseTransport<SqlServerTransport>()
                .Transactions(TransportTransactionMode.TransactionScope)
                .ConnectionString(ConnectionString);

            configuration.UsePersistence<NHibernatePersistence>()
                .ConnectionString(ConnectionString);

            context = new Context();

            configuration.RegisterComponents(c => c.ConfigureComponent(() => context, DependencyLifecycle.SingleInstance));

            configuration.Pipeline.Register<BehaviorThatThrowsAfterFirstMessage.Registration>();

            endpoint = Endpoint.Start(configuration).GetAwaiter().GetResult();
        }

        [Test]
        public async Task Transport_transaction_should_be_reused_by_nhibernate_persistence()
        {
            var options = new SendOptions();
            options.SetMessageId(context.Id.ToString());
            options.RouteToLocalEndpointInstance();

            await endpoint.Send(new SagaMessage { Id = context.Id }, options);

            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(20)), context.CompletionSource.Task);

            Assert.IsFalse(context.TransactionEscalatedToDTC, "Transaction should not be escalated to DTC");

            Assert.AreEqual(2, context.SagaHandlerInvocationNumber, "Saga handler should be called twice");
            Assert.AreEqual(1, context.SagaCounterValue, "Saga value should be incremented only once");
        }

        public class Context
        {
            public readonly Guid Id = Guid.NewGuid();

            public int SagaCounterValue { get; set; }

            public int SagaHandlerInvocationNumber { get; set; }

            public bool TransactionEscalatedToDTC { get; set; }

            public TaskCompletionSource<int> CompletionSource = new TaskCompletionSource<int>();
        }

        public class TestSaga : Saga<SagaData>, IAmStartedByMessages<SagaMessage>
        {
            public Context TestContext { get; set; }

            public async Task Handle(SagaMessage message, IMessageHandlerContext context)
            {
                Data.SomeId = message.Id;

                if (message.Id == TestContext.Id)
                {
                    Data.Counter += 1;

                    TestContext.SagaCounterValue = Data.Counter;
                    TestContext.SagaHandlerInvocationNumber++;

                    await context.SendLocal(new ReplyMessage {Id = message.Id});
                }
            }

            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
            {
                mapper.ConfigureMapping<SagaMessage>(m => m.Id)
                    .ToSaga(s => s.SomeId);
            }
        }

        class BehaviorThatThrowsAfterFirstMessage : Behavior<ITransportReceiveContext>
        {
            public Context TestContext { get; set; }

            public async override Task Invoke(ITransportReceiveContext context, Func<Task> next)
            {
                await next();

                if (context.Message.MessageId == TestContext.Id.ToString() && TestContext.SagaHandlerInvocationNumber == 1)
                {
                    TestContext.TransactionEscalatedToDTC = Transaction.Current.TransactionInformation.DistributedIdentifier != Guid.Empty;

                    throw new Exception("Simulated exception after saga processing is done");
                }
            }

            public class Registration : RegisterStep
            {
                public Registration() : base("BehaviorThatThrowsAfterFirstMessage", typeof(BehaviorThatThrowsAfterFirstMessage), "BehaviorThatThrowsAfterFirstMessage")
                {
                    InsertAfter("FirstLevelRetries");
                }
            }
        }

        public class Handler : IHandleMessages<ReplyMessage>
        {
            public Context TestContext { get; set; }

            public Task Handle(ReplyMessage message, IMessageHandlerContext context)
            {
                if (TestContext.Id == message.Id)
                {
                    TestContext.CompletionSource.SetResult(0);
                }

                return Task.FromResult(0);
            }
        }

        public class SagaData : ContainSagaData
        {
            public virtual Guid SomeId { get; set; }
            public virtual int Counter { get; set; }
        }

        public class SagaMessage : IMessage
        {
            public Guid Id { get; set; }
        }

        public class ReplyMessage : IMessage
        {
            public Guid Id { get; set; }
        }
    }
}