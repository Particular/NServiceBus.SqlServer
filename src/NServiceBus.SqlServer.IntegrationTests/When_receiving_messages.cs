﻿namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
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
                qa => qa.TableName == "input" ? (TableBasedQueue)inputQueue : new TableBasedQueue(qa), 
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

            await WaitUntil(() => inputQueue.NumberOfPeeks > 1);

            await pump.Stop();

            Assert.That(inputQueue.NumberOfReceives, Is.AtMost(successfulReceives + 2), "Pump should stop receives after first unsuccessful attempt.");
        }

        static async Task WaitUntil(Func<bool> condition, int timeoutInSeconds = 5)
        {
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(timeoutInSeconds))
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            throw new Exception("Condition has not been met in predefined timespan.");
        }

        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");

        class FakeTableBasedQueue : TableBasedQueue
        {
            public int NumberOfReceives { get; set; }
            public int NumberOfPeeks { get; set; }

            int queueSize;
            int successfulReceives;

            public FakeTableBasedQueue(QueueAddress address, int queueSize, int successfulReceives) : base(address)
            {
                this.queueSize = queueSize;
                this.successfulReceives = successfulReceives;
            }

            public override  Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
            {
                NumberOfReceives ++;

                var readResult = NumberOfReceives <= successfulReceives
                    ? MessageReadResult.Success(new Message("1", new Dictionary<string, string>(), new MemoryStream()))
                    : MessageReadResult.NoMessage;

                return Task.FromResult(readResult);
            }

            public override Task<int> TryPeek(SqlConnection connection, CancellationToken token)
            {
                NumberOfPeeks ++;

                return Task.FromResult(NumberOfPeeks == 1 ? queueSize : 0);
            }
        }
    }
}