﻿namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport;
    using SqlServer;

    public class When_receiving_messages
    {
        [Test]
        public async Task Should_stop_pumping_messages_after_first_unsuccessful_receive()
        {
            var successfulReceives = 46;
            var queueSize = 1000;

            var parser = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            var inputQueue = new FakeTableBasedQueue(parser.Parse("input").QualifiedTableName, queueSize, successfulReceives);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
            }

            var sqlConnectionFactory = SqlConnectionFactory.Default(connectionString);

            var pump = new MessagePump(
                m => new ProcessWithNoTransaction(sqlConnectionFactory, null),
                qa => qa == "input" ? (TableBasedQueue)inputQueue : new TableBasedQueue(parser.Parse(qa).QualifiedTableName, qa),
                new QueuePurger(sqlConnectionFactory),
                new ExpiredMessagesPurger(_ => sqlConnectionFactory.OpenNewConnection(), 0, false),
                new QueuePeeker(sqlConnectionFactory, new QueuePeekerOptions()),
                new SchemaInspector(_ => sqlConnectionFactory.OpenNewConnection()),
                TimeSpan.MaxValue);

            await pump.Init(
                _ => Task.FromResult(0),
                _ => Task.FromResult(ErrorHandleResult.Handled),
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

        class FakeTableBasedQueue : TableBasedQueue
        {
            public int NumberOfReceives { get; set; }
            public int NumberOfPeeks { get; set; }

            int queueSize;
            int successfulReceives;

            public FakeTableBasedQueue(string address, int queueSize, int successfulReceives) : base(address, "")
            {
                this.queueSize = queueSize;
                this.successfulReceives = successfulReceives;
            }

            public override  Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
            {
                NumberOfReceives ++;

                var readResult = NumberOfReceives <= successfulReceives
                    ? MessageReadResult.Success(new Message("1", new Dictionary<string, string>(), new byte[0], false))
                    : MessageReadResult.NoMessage;

                return Task.FromResult(readResult);
            }

            public override Task<int> TryPeek(SqlConnection connection, SqlTransaction transaction, CancellationToken token, int timeoutInSeconds = 30)
            {
                NumberOfPeeks ++;

                return Task.FromResult(NumberOfPeeks == 1 ? queueSize : 0);
            }
        }
    }
}