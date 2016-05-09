namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NUnit.Framework;
    using Transports;
    using Transports.SQLServer;
    using static System.String;
    using IsolationLevel = System.Transactions.IsolationLevel;

    [TestFixture]
    class ReceiveStrategyTests
    {

        [TestCaseSource(nameof(TestCases))]
        public async Task It_should_set_dispatch_strategy(ReceiveStrategy receiveStrategy, TransportTransactionMode transactionMode)
        {
            var received = false;

            await SendMessage();

            await receiveStrategy.ReceiveMessage(queue, errorQueue, new CancellationTokenSource(), context =>
            {
                IDispatchStrategy dispatchStrategy;
                Assert.IsTrue(context.TransportTransaction.TryGet(out dispatchStrategy));
                received = true;
                return Task.FromResult(0);
            });
            Assert.IsTrue(received);
            Assert.AreEqual(0, await queuePurger.Purge(queue)); //No messages in the input queue
        }

        [TestCaseSource(nameof(TestCases))]
        public async Task It_should_dead_letter_and_delete_unparsable_messages(ReceiveStrategy receiveStrategy, TransportTransactionMode transactionMode)
        {
            var received = false;

            await SendPoisonMessage();

            await receiveStrategy.ReceiveMessage(queue, errorQueue, new CancellationTokenSource(), context =>
            {
                received = true;
                return Task.FromResult(0);
            });
            Assert.IsFalse(received);
            Assert.AreEqual(0, await queuePurger.Purge(queue)); //No messages in the input queue
            Assert.AreEqual(1, await queuePurger.Purge(errorQueue)); //A message should be in the error queue.
        }

        [TestCaseSource(nameof(TestCases))]
        public async Task It_should_break_receive_loop_after_unsuccessful_receive(ReceiveStrategy receiveStrategy, TransportTransactionMode transactionMode)
        {
            var received = false;

            var receiveCancellationTokenSource = new CancellationTokenSource();
            await receiveStrategy.ReceiveMessage(queue, errorQueue, receiveCancellationTokenSource, context =>
            {
                received = true;
                return Task.FromResult(0);
            });
            Assert.IsFalse(received);
            Assert.IsTrue(receiveCancellationTokenSource.IsCancellationRequested);
        }

        [TestCaseSource(nameof(TestCases))]
        public async Task It_should_not_remove_message_from_queue_if_receive_has_been_cancelled(ReceiveStrategy receiveStrategy, TransportTransactionMode transactionMode)
        {
            if (transactionMode == TransportTransactionMode.None)
            {
                Assert.Pass();
            }

            await SendMessage();
            await receiveStrategy.ReceiveMessage(queue, errorQueue, new CancellationTokenSource(), context =>
            {
                context.ReceiveCancellationTokenSource.Cancel();
                return Task.FromResult(0);
            });
            Assert.AreEqual(1, await queuePurger.Purge(queue)); //Message should still be in the queue
        }

        [TestCaseSource(nameof(TestCases))]
        public async Task It_should_remove_message_from_queue_after_successful_processing(ReceiveStrategy receiveStrategy, TransportTransactionMode transactionMode)
        {
            var received = false;

            await SendMessage();
            await receiveStrategy.ReceiveMessage(queue, errorQueue, new CancellationTokenSource(), context =>
            {
                received = true;
                return Task.FromResult(0);
            });
            Assert.IsTrue(received);
            Assert.AreEqual(0, await queuePurger.Purge(queue)); //Message removed from the input queue
        }

        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
        static readonly TransactionOptions transactionOptions = new TransactionOptions()
        {
            IsolationLevel = IsolationLevel.ReadCommitted
        };

        const string queueName = "ReceiveStrategyTests";
        const string errorQueueName = "ReceiveStrategyTests.DLQ";

        static object[] TestCases =
        {
            new object[] { new ReceiveWithNoTransaction(sqlConnectionFactory), TransportTransactionMode.None},
            new object[] { new ReceiveWithReceiveOnlyTransaction(transactionOptions, sqlConnectionFactory), TransportTransactionMode.None},
            new object[] { new ReceiveWithSendsAtomicWithReceiveTransaction(transactionOptions, sqlConnectionFactory), TransportTransactionMode.TransactionScope},
            new object[] { new ReceiveWithTransactionScope(transactionOptions, sqlConnectionFactory), TransportTransactionMode.TransactionScope},
            new object[] { new LegacyReceiveWithTransactionScope(transactionOptions, new LegacySqlConnectionFactory(_ => sqlConnectionFactory.OpenNewConnection())), TransportTransactionMode.TransactionScope}
        };


        async Task SendMessage()
        {
            var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await sqlConnectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                await queue.Send(message, connection, null).ConfigureAwait(false);
                scope.Complete();
            }
        }
        async Task SendPoisonMessage()
        {
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await sqlConnectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                var commandText = Format(Sql.SendText, "dbo", queueName);

                using (var command = new SqlCommand(commandText, connection, null))
                {
                    command.Parameters.Add("Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
                    command.Parameters.Add("CorrelationId", SqlDbType.VarChar).Value = "Correlation";
                    command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value = "ReplyTo";
                    command.Parameters.Add("Recoverable", SqlDbType.Bit).Value = true;
                    command.Parameters.Add("TimeToBeReceivedMs", SqlDbType.Int).Value = DBNull.Value;
                    command.Parameters.Add("Headers", SqlDbType.VarChar).Value = "<InvalidJson/>";
                    command.Parameters.Add("Body", SqlDbType.VarBinary).Value = new byte[0];

                    await command.ExecuteNonQueryAsync();
                }
                scope.Complete();
            }
        }

        [SetUp]
        public void Prepare()
        {
            PrepareAsync().GetAwaiter().GetResult();
        }

        async Task PrepareAsync()
        {
            var addressParser = new QueueAddressParser("dbo", null, s => null);

            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(queueName);
            queueBindings.BindReceiving(errorQueueName);
            
            queue = new TableBasedQueue(addressParser.Parse(queueName));
            errorQueue = new TableBasedQueue(addressParser.Parse(errorQueueName));

            creator = new QueueCreator(sqlConnectionFactory, addressParser);
            queuePurger = new QueuePurger(sqlConnectionFactory);

            await creator.CreateQueueIfNecessary(queueBindings, "");
            await queuePurger.Purge(queue);
            await queuePurger.Purge(errorQueue);
        }

        TableBasedQueue queue;
        TableBasedQueue errorQueue;
        QueuePurger queuePurger;
        QueueCreator creator;
    }
}