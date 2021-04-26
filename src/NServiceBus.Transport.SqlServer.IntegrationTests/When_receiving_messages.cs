namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Linq;
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
        public async Task Should_stop_receiving_messages_after_first_unsuccessful_receive()
        {
            var successfulReceives = 46;
            var queueSize = 1000;

            var parser = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            var inputQueueAddress = parser.Parse("input").Address;
            var inputQueue = new FakeTableBasedQueue(inputQueueAddress, queueSize, successfulReceives);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
            }

            var transport = new SqlServerTransport(SqlConnectionFactory.Default(connectionString).OpenNewConnection)
            {
                TransportTransactionMode = TransportTransactionMode.None,
                TimeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.MaxValue,
            };

            transport.Testing.QueueFactoryOverride = qa =>
                qa == inputQueueAddress ? inputQueue : new TableBasedQueue(parser.Parse(qa).QualifiedTableName, qa, true);

            var receiveSettings = new ReceiveSettings("receiver", inputQueueAddress, true, false, "error");
            var hostSettings = new HostSettings("IntegrationTests", string.Empty, new StartupDiagnosticEntries(),
                (_, __, ___) => { },
                true);

            var infrastructure = await transport.Initialize(hostSettings, new[] { receiveSettings }, new string[0]);

            var receiver = infrastructure.Receivers.First().Value;

            await receiver.Initialize(
                new PushRuntimeSettings(1),
                (_, __) => Task.CompletedTask,
                (_, __) => Task.FromResult(ErrorHandleResult.Handled));

            await receiver.StartReceive();

            await WaitUntil(() => inputQueue.NumberOfPeeks > 1);

            await receiver.StopReceive();

            await infrastructure.Shutdown();

            Assert.That(inputQueue.NumberOfReceives, Is.AtMost(successfulReceives + 2), "Receiver should stop receives after first unsuccessful attempt.");
        }

        static async Task WaitUntil(Func<bool> condition, int timeoutInSeconds = 5, CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow; //Local usage only

            while (DateTime.UtcNow.Subtract(startTime) < TimeSpan.FromSeconds(timeoutInSeconds))
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
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

            public override Task<MessageReadResult> TryReceive(SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken = default)
            {
                NumberOfReceives++;

                var readResult = NumberOfReceives <= successfulReceives
                    ? MessageReadResult.Success(new Message("1", string.Empty, null, new byte[0], false))
                    : MessageReadResult.NoMessage;

                return Task.FromResult(readResult);
            }

            public override Task<int> TryPeek(SqlConnection connection, SqlTransaction transaction, int? timeoutInSeconds = null, CancellationToken cancellationToken = default)
            {
                NumberOfPeeks++;

                return Task.FromResult(NumberOfPeeks == 1 ? queueSize : 0);
            }
        }
    }
}