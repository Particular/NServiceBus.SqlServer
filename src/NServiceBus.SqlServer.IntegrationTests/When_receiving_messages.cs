namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transports;
    using Transports.SQLServer;

    public class When_receiving_messages
    {
        [Test]
        public async Task Should_stop_pumping_messages_after_first_unsuccessful_receive()
        {
            var successfulReceives = 46;
            var queueSize = 1000;

            var inputQueue = new FakeTableBasedQueue(QueueAddress.Parse("dbo.input"), queueSize, successfulReceives);

            var pump = new MessagePump(
                m => new ReceiveWithNoTransaction(sqlConnectionFactory),
                qa => qa.TableName == "input" ? (ITableBasedQueue)inputQueue : new TableBasedQueue(qa), 
                new QueuePurger(sqlConnectionFactory),
                new ExpiredMessagesPurger(_ => sqlConnectionFactory.OpenNewConnection(), TimeSpan.MaxValue, 0),
                new QueuePeeker(sqlConnectionFactory),
                new QueueAddressParser("dbo", null, null),
                TimeSpan.MaxValue);

            await pump.Init(
                _ => Task.FromResult(0),
                new CriticalError(_ => Task.FromResult(0)),
                new PushSettings("input", "error", false, TransportTransactionMode.None));

            pump.Start(new PushRuntimeSettings(1));

            while (inputQueue.NumberOfPeeks < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            await pump.Stop();

            Assert.AreEqual(successfulReceives + 2, inputQueue.NumberOfReceives, "Pump should stop receives after first unsuccessful attempt.");
        }


        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");

        class FakeTableBasedQueue : ITableBasedQueue
        {
            public int NumberOfReceives { get; set; }
            public int NumberOfPeeks { get; set; }

            QueueAddress queue;
            int queueSize;
            int successfulReceives;

            public FakeTableBasedQueue(QueueAddress queue, int queueSize, int successfulReceives)
            {
                this.queue = queue;

                this.queueSize = queueSize;
                this.successfulReceives = successfulReceives;
            }

            public string TransportAddress => queue.ToString();

            public Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
            {
                NumberOfReceives ++;

                var readResult = NumberOfReceives <= successfulReceives
                    ? MessageReadResult.Success(new Message("1", null, new Dictionary<string, string>(), new MemoryStream()))
                    : MessageReadResult.NoMessage;

                return Task.FromResult(readResult);
            }

            public Task SendMessage(OutgoingMessage message, SqlConnection connection, SqlTransaction transaction)
            {
                throw new NotImplementedException();
            }

            public Task SendRawMessage(object[] data, SqlConnection connection, SqlTransaction transaction)
            {
                throw new NotImplementedException();
            }

            public Task<int> TryPeek(SqlConnection connection, CancellationToken token)
            {
                NumberOfPeeks ++;

                return Task.FromResult(NumberOfPeeks == 1 ? queueSize : 0);
            }

            public Task<int> Purge(SqlConnection connection)
            {
                throw new NotImplementedException();
            }

            public Task<int> PurgeBatchOfExpiredMessages(SqlConnection connection, int purgeBatchSize)
            {
                return Task.FromResult(0);
            }

            public Task LogWarningWhenIndexIsMissing(SqlConnection connection)
            {
                return Task.FromResult(0);
            }
        }
    }
}