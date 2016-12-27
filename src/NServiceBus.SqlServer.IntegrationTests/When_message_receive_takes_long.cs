namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using NUnit.Framework;
    using Transport.SQLServer;
    using Transport;

    public class When_message_receive_takes_long
    {
        const string QueueTableName = "LongRunningTasksQueue";
        const int ReceiveDelayInSeconds = 6;
        const int PeekTimeoutInSeconds = 2;

        TableBasedQueue queue;

        [SetUp]
        public async Task SetUp()
        {
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            await CreateQueueIfNotExists(addressParser);

            queue = new TableBasedQueue(addressParser.Parse(QueueTableName).QualifiedTableName, QueueTableName);
        }

        [Test]
        public async Task It_does_not_block_queue_peeking()
        {
            await SendMessage(queue);

            var receiveTask = ReceiveWithLongHandling(queue);

            Assert.DoesNotThrowAsync(async () => { await TryPeekQueueSize(queue);});

            await receiveTask;
        }

        static async Task SendMessage(TableBasedQueue tableBasedQueue)
        {
            await ExecuteInTransactionScope(async c =>
            {
                var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);

                await tableBasedQueue.Send(message.Headers, message.Body, c, null);
            });
        }

        static async Task TryPeekQueueSize(TableBasedQueue tableBasedQueue)
        {
            await ExecuteInTransactionScope(async c => {
                await tableBasedQueue.TryPeek(c, CancellationToken.None, PeekTimeoutInSeconds);
            });
        }

        static async Task ReceiveWithLongHandling(TableBasedQueue tableBasedQueue)
        {
            await ExecuteInTransactionScope(async c => {
                await tableBasedQueue.TryReceive(c, null);

                await Task.Delay(TimeSpan.FromSeconds(ReceiveDelayInSeconds));
            });
        }

        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");


        static Task CreateQueueIfNotExists(QueueAddressTranslator addressTranslator)
        {
            var queueCreator = new QueueCreator(sqlConnectionFactory, addressTranslator);
            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(QueueTableName);

            return queueCreator.CreateQueueIfNecessary(queueBindings, "");
        }

        static async Task ExecuteInTransactionScope(Func<SqlConnection, Task> operations)
        {
            var transactionOptions = new TransactionOptions
            {
                IsolationLevel = IsolationLevel.ReadCommitted
            };

            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await sqlConnectionFactory.OpenNewConnection())
                {
                    await operations(connection);

                    scope.Complete();
                }
            }
        }
    }
}