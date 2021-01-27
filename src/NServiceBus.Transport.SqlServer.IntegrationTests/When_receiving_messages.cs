using System.Collections.Generic;
using NServiceBus.Unicast.Messages;

namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
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
            
            var transport = new SqlServerTransport
            {
                TransportTransactionMode = TransportTransactionMode.None,
                TimeToWaitBeforeTriggering = TimeSpan.MaxValue,
                ConnectionFactory = SqlConnectionFactory.Default(connectionString).OpenNewConnection,
                
                QueueFactoryOverride = qa => qa == "input" ? inputQueue : new TableBasedQueue(parser.Parse(qa).QualifiedTableName, qa, true),
            };

            var receiver = new ReceiveSettings("receiver", parser.Parse("input").Address, true, false, "error");
            var hostSettings = new HostSettings("IntegrationTests", string.Empty, new StartupDiagnosticEntries(),
                (_, __) => { },
                true);

            var infrastructure = await transport.Initialize(hostSettings, new[] {receiver},new string[0]);

            var pump = infrastructure.Receivers[0];

            await pump.Initialize(
                new PushRuntimeSettings(1),
                _ => Task.CompletedTask,
                _ => Task.FromResult(ErrorHandleResult.Handled),
                new List<MessageMetadata>()
            );

            await pump.StartReceive();

            await WaitUntil(() => inputQueue.NumberOfPeeks > 1);

            await pump.StartReceive();

            await infrastructure.DisposeAsync();

            Assert.That(inputQueue.NumberOfReceives, Is.AtMost(successfulReceives + 2), "Pump should stop receives after first unsuccessful attempt.");
        }

        static async Task WaitUntil(Func<bool> condition, int timeoutInSeconds = 5)
        {
            var startTime = DateTime.UtcNow; //Local usage only

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

            public FakeTableBasedQueue(string address, int queueSize, int successfulReceives) : base(address, "", true)
            {
                this.queueSize = queueSize;
                this.successfulReceives = successfulReceives;
            }

            public override Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction)
            {
                NumberOfReceives++;

                var readResult = NumberOfReceives <= successfulReceives
                    ? MessageReadResult.Success(new Message("1", string.Empty, null, new byte[0], false))
                    : MessageReadResult.NoMessage;

                return Task.FromResult(readResult);
            }

            public override Task<int> TryPeek(SqlConnection connection, SqlTransaction transaction, CancellationToken token, int timeoutInSeconds = 30)
            {
                NumberOfPeeks++;

                return Task.FromResult(NumberOfPeeks == 1 ? queueSize : 0);
            }
        }
    }
}