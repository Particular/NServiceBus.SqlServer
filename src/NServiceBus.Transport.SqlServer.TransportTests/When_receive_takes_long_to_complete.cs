#pragma warning disable PS0018
namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport;
    using Transport.SqlServer;

    public class When_receive_takes_long_to_complete : NServiceBusTransportTest
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Peeker_should_provide_accurate_queue_length_estimate(TransportTransactionMode transactionMode)
        {
            var connectionFactory = new SqlServerDbConnectionFactory(ConfigureSqlServerTransportInfrastructure.ConnectionString);

            SqlTableBasedQueue queue = await CreateATestQueue(connectionFactory);

            await SendAMessage(connectionFactory, queue);
            await SendAMessage(connectionFactory, queue);

            var (txStarted, txFinished, txCompletionSource) = SpawnALongRunningReceiveTransaction(connectionFactory, queue);

            await txStarted;

            var peekCount = 0;

            using (var connection = await connectionFactory.OpenNewConnection())
            {
                var transaction = await connection.BeginTransactionAsync();

                queue.FormatPeekCommand();
                peekCount = await queue.TryPeek(connection, transaction);
            }

            txCompletionSource.SetResult();
            await txFinished;

            Assert.That(peekCount, Is.EqualTo(1), "A long running receive transaction should not skew the estimation for number of messages in the queue.");
        }

        static async Task<SqlTableBasedQueue> CreateATestQueue(SqlServerDbConnectionFactory connectionFactory)
        {
            var queueName = "queue_length_estimation_test";

            var sqlConstants = new SqlServerConstants();

            var queue = new SqlTableBasedQueue(sqlConstants, queueName, queueName, false);

            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator.Parse, false);

            await queueCreator.CreateQueueIfNecessary(new[] { queueName }, null);

            await using var connection = await connectionFactory.OpenNewConnection();
            await queue.Purge(connection);

            return queue;
        }

        static async Task SendAMessage(SqlServerDbConnectionFactory connectionFactory, SqlTableBasedQueue queue)
        {
            await using var connection = await connectionFactory.OpenNewConnection();
            var transaction = await connection.BeginTransactionAsync();

            await queue.Send(new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>()), TimeSpan.MaxValue, connection, transaction);

            await transaction.CommitAsync();
        }

        (Task, Task, TaskCompletionSource) SpawnALongRunningReceiveTransaction(SqlServerDbConnectionFactory connectionFactory, SqlTableBasedQueue queue)
        {
            var started = new TaskCompletionSource();
            var cancellationTokenSource = new TaskCompletionSource();

            var task = Task.Run(async () =>
            {
                await using var connection = await connectionFactory.OpenNewConnection();
                var transaction = await connection.BeginTransactionAsync();

                await queue.TryReceive(connection, transaction);

                started.SetResult();

                await cancellationTokenSource.Task;
            });

            return (started.Task, task, cancellationTokenSource);
        }
    }
}