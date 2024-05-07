namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NUnit.Framework;
    using Sql.Shared.Queuing;
    using Transport;
    using SqlServer;

    public class When_receiving_messages
    {
        SqlServerConstants sqlConstants = new();

        [Test]
        public async Task Should_stop_receiving_messages_after_first_unsuccessful_receive()
        {
            var successfulReceives = 46;
            var queueSize = 1000;

            var parser = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            var inputQueueName = "input";
            var inputQueueAddress = parser.Parse(inputQueueName).Address;
            var inputQueue = new FakeTableBasedQueue(sqlConstants, inputQueueAddress, queueSize, successfulReceives);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            var transport = new SqlServerTransport(async (ct) =>
            {
                var factory = new SqlServerDbConnectionFactory(connectionString);
                var connection = await factory.OpenNewConnection(ct);

                //TODO: get rid of casting
                return (SqlConnection)connection;
            })
            {
                TransportTransactionMode = TransportTransactionMode.None,
                TimeToWaitBeforeTriggeringCircuitBreaker = TimeSpan.MaxValue,
            };

            transport.Testing.QueueFactoryOverride = qa =>
                qa == inputQueueAddress
                    ? inputQueue
                    : new SqlTableBasedQueue(sqlConstants, parser.Parse(qa).QualifiedTableName, qa, true);

            var receiveSettings = new ReceiveSettings("receiver", new Transport.QueueAddress(inputQueueName), true, false, "error");
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

            public FakeTableBasedQueue(ISqlConstants sqlConstants, string address, int queueSize, int successfulReceives) : base(sqlConstants, address, "", true)
            {
                this.queueSize = queueSize;
                this.successfulReceives = successfulReceives;
            }

            public override Task<MessageReadResult> TryReceive(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
            {
                NumberOfReceives++;

                var readResult = NumberOfReceives <= successfulReceives
                    ? MessageReadResult.Success(new Message("1", string.Empty, new byte[0], false))
                    : MessageReadResult.NoMessage;

                return Task.FromResult(readResult);
            }

            protected override Task SendRawMessage(MessageRow message, DbConnection connection, DbTransaction transaction,
                CancellationToken cancellationToken = default) =>
                throw new NotImplementedException(); //TODO: implement :)

            public override Task<int> TryPeek(DbConnection connection, DbTransaction transaction, int? timeoutInSeconds = null, CancellationToken cancellationToken = default)
            {
                NumberOfPeeks++;

                return Task.FromResult(NumberOfPeeks == 1 ? queueSize : 0);
            }
        }
    }
}