namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Collections.Generic;
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

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
            }

            sqlConnectionFactory = SqlConnectionFactory.Default(connectionString);

            await CreateQueueIfNotExists(addressParser, sqlConnectionFactory);

            queue = new TableBasedQueue(addressParser.Parse(QueueTableName).QualifiedTableName, QueueTableName);
        }

        [Test]
        public async Task It_does_not_block_queue_peeking()
        {
            await SendMessage(queue, sqlConnectionFactory);

            var receiveTask = ReceiveWithLongHandling(queue, sqlConnectionFactory);

            Assert.DoesNotThrowAsync(async () => { await TryPeekQueueSize(queue, sqlConnectionFactory);});

            await receiveTask;
        }

        static async Task SendMessage(TableBasedQueue tableBasedQueue, SqlConnectionFactory sqlConnectionFactory)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await sqlConnectionFactory.OpenNewConnection())
                using (var tx = connection.BeginTransaction())
                {
                    var message = new OutgoingMessage(Guid.NewGuid().ToString(), new Dictionary<string, string>(), new byte[0]);
                    await tableBasedQueue.Send(message, TimeSpan.MaxValue, connection, tx);
                    tx.Commit();
                    scope.Complete();
                }
            }
        }

        static async Task TryPeekQueueSize(TableBasedQueue tableBasedQueue, SqlConnectionFactory sqlConnectionFactory)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await sqlConnectionFactory.OpenNewConnection())
                using (var tx = connection.BeginTransaction())
                {
                    await tableBasedQueue.TryPeek(connection, tx, CancellationToken.None, PeekTimeoutInSeconds);
                    scope.Complete();
                }
            }
        }

        static async Task ReceiveWithLongHandling(TableBasedQueue tableBasedQueue, SqlConnectionFactory sqlConnectionFactory)
        {
            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            {
                using (var connection = await sqlConnectionFactory.OpenNewConnection())
                using (var tx = connection.BeginTransaction())
                {
                    await tableBasedQueue.TryReceive(connection, tx);
                    await Task.Delay(TimeSpan.FromSeconds(ReceiveDelayInSeconds));
                    tx.Commit();
                    scope.Complete();
                }
            }
        }

        SqlConnectionFactory sqlConnectionFactory;

        static Task CreateQueueIfNotExists(QueueAddressTranslator addressTranslator, SqlConnectionFactory sqlConnectionFactory)
        {
            var queueCreator = new QueueCreator(sqlConnectionFactory, addressTranslator);
            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(QueueTableName);

            return queueCreator.CreateQueueIfNecessary(queueBindings, "");
        }
    }
}