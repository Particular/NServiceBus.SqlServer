namespace NServiceBus.TransportTests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport;
    using Transport.PostgreSql;

    public class When_onMessage_takes_long_time_to_complete : NServiceBusTransportTest
    {
        //[TestCase(TransportTransactionMode.None)]
        //[TestCase(TransportTransactionMode.ReceiveOnly)]
        //[TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.TransactionScope)]
        public async Task Should_expose_receiving_address(TransportTransactionMode transactionMode)
        {
            var connectionString = @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Connection Lifetime=0;";

            var postgreSqlConstants = new PostgreSqlConstants();
            var connectionFactory = new PostgreSqlDbConnectionFactory(connectionString);
            var addressTranslator = new QueueAddressTranslator("public", null, new QueueSchemaOptions());
            var queueName = "test";

            var queue = new PostgreSqlTableBasedQueue(postgreSqlConstants, "test", "test", false);

            var queueCreator = new QueueCreator(postgreSqlConstants, connectionFactory, addressTranslator.Parse, false);

            await queueCreator.CreateQueueIfNecessary(new[] { queueName }, null);

            using (var connection = await connectionFactory.OpenNewConnection())
            {
                await queue.Purge(connection);
            }

            for (var i = 0; i < 2; i++)
            {
                using (var connection = await connectionFactory.OpenNewConnection())
                {
                    var transaction = await connection.BeginTransactionAsync();

                    await queue.Send(new OutgoingMessage(i.ToString(), [], Array.Empty<byte>()), TimeSpan.MaxValue, connection, transaction);

                    await transaction.CommitAsync();
                }
            }

            await StartReceiveTransaction(connectionFactory, queue);

            //Peek number of available messages in the queue
            var peekCount = 0;

            using (var connection = await connectionFactory.OpenNewConnection())
            {
                var transaction = await connection.BeginTransactionAsync();

                queue.FormatPeekCommand();
                peekCount = await queue.TryPeek(connection, transaction);
            }

            Assert.Equals(1, peekCount);
        }

        Task StartReceiveTransaction(PostgreSqlDbConnectionFactory connectionFactory,
            PostgreSqlTableBasedQueue queue)
        {
            var tcs = new TaskCompletionSource();

            var _ = Task.Run(async () =>
            {
                using (var connection = await connectionFactory.OpenNewConnection())
                {
                    var transaction = await connection.BeginTransactionAsync();

                    await queue.TryReceive(connection, transaction);

                    tcs.SetResult();

                    await Task.Delay(TimeSpan.FromDays(1));
                }
            });

            return tcs.Task;
        }
    }
}