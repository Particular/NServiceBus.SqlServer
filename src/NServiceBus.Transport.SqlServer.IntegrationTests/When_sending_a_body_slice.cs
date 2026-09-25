namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using SqlServer;
    using Transport;

    public class When_sending_a_body_slice
    {
        [TestCase(0, true)]
        [TestCase(100, true)]
        [TestCase(8000, true)]
        [TestCase(8001, true)]
        [TestCase(200_000, true)]
        [TestCase(100, false)]
        [TestCase(200_000, false)]
        public async Task Should_store_only_the_slice(int bodyLength, bool isStreamSupported)
        {
            var queue = new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, isStreamSupported);
            var buffer = new byte[bodyLength + 64];
            Random.Shared.NextBytes(buffer);
            var body = buffer.AsMemory(13, bodyLength);

            await using var connection = await dbConnectionFactory.OpenNewConnection();
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await queue.Send(new OutgoingMessage("1", [], body), TimeSpan.MaxValue, connection, transaction);
                await transaction.CommitAsync();
            }

            var result = await queue.TryReceive(connection, null, 0);

            Assert.That(result.Message.Body.ToArray(), Is.EqualTo(body.ToArray()).AsCollection);
        }

        [TestCase(100)]
        [TestCase(200_000)]
        public async Task Should_store_only_the_slice_for_delayed_delivery(int bodyLength)
        {
            var queue = new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, true);
            var delayedMessageTable = new DelayedMessageTable(sqlConstants, delayedQueueAddress.QualifiedTableName, new MoveDueDelayedMessagesCommand(sqlConstants, delayedQueueAddress.QualifiedTableName, queueAddress.QualifiedTableName));
            var buffer = new byte[bodyLength + 64];
            Random.Shared.NextBytes(buffer);
            var body = buffer.AsMemory(13, bodyLength);

            await using var connection = await dbConnectionFactory.OpenNewConnection();
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await delayedMessageTable.Store(new OutgoingMessage("1", [], body), TimeSpan.Zero, QueueName, connection, transaction);
                await transaction.CommitAsync();
            }

            var result = MessageReadResult.NoMessage;
            for (var attempt = 0; attempt < 20 && result.IsEmpty; attempt++)
            {
                await Task.Delay(50);
                await using var transaction = await connection.BeginTransactionAsync();
                await delayedMessageTable.MoveDueMessages(10, connection, transaction);
                result = await queue.TryReceive(connection, transaction, 0);
                await transaction.CommitAsync();
            }

            Assert.That(result.Message.Body.ToArray(), Is.EqualTo(body.ToArray()).AsCollection);
        }

        [SetUp]
        public async Task SetUp()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, new QueueSchemaAndCatalogOptions());
            queueAddress = addressTranslator.Parse(QueueName);
            delayedQueueAddress = new CanonicalQueueAddress(QueueName + ".Delayed", "dbo", "nservicebus");

            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);
            await queueCreator.CreateQueueIfNecessary([QueueName], delayedQueueAddress, CancellationToken.None);

            await new QueuePurger(dbConnectionFactory).Purge(new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, true));

            await using var connection = await dbConnectionFactory.OpenNewConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DELETE FROM {delayedQueueAddress.QualifiedTableName}";
            await command.ExecuteNonQueryAsync();
        }

        readonly SqlServerConstants sqlConstants = new();
        SqlServerDbConnectionFactory dbConnectionFactory;
        CanonicalQueueAddress queueAddress;
        CanonicalQueueAddress delayedQueueAddress;

        const string QueueName = "BodySliceTests";
    }
}
