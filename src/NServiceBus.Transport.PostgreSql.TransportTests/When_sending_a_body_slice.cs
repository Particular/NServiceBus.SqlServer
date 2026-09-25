namespace NServiceBus.TransportTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Transport;
using Transport.Sql.Shared;
using Transport.PostgreSql;

public class When_sending_a_body_slice
{
    [TestCase(0)]
    [TestCase(100)]
    [TestCase(200_000)]
    public async Task Should_store_only_the_slice(int bodyLength)
    {
        var buffer = new byte[bodyLength + 64];
        Random.Shared.NextBytes(buffer);
        var body = buffer.AsMemory(13, bodyLength);

        await using var connection = await connectionFactory.OpenNewConnection(CancellationToken.None);
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await queue.Send(new OutgoingMessage("1", [], body), TimeSpan.MaxValue, connection, transaction, CancellationToken.None);
            await transaction.CommitAsync();
        }

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var result = await queue.TryReceive(connection, transaction, 0, CancellationToken.None);
            await transaction.CommitAsync();

            Assert.That(result.Message.Body.ToArray(), Is.EqualTo(body.ToArray()).AsCollection);
        }
    }

    [TestCase(100)]
    [TestCase(200_000)]
    public async Task Should_store_only_the_slice_for_delayed_delivery(int bodyLength)
    {
        var sqlConstants = new PostgreSqlConstants();
        var delayedMessageTable = new DelayedMessageTable(sqlConstants, delayedQueueAddress.QualifiedTableName, new MoveDueDelayedMessagesCommand(sqlConstants, delayedQueueAddress.QualifiedTableName, queueAddress.QualifiedTableName));
        var buffer = new byte[bodyLength + 64];
        Random.Shared.NextBytes(buffer);
        var body = buffer.AsMemory(13, bodyLength);

        await using var connection = await connectionFactory.OpenNewConnection(CancellationToken.None);
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await delayedMessageTable.Store(new OutgoingMessage("1", [], body), TimeSpan.Zero, QueueName, connection, transaction, CancellationToken.None);
            await transaction.CommitAsync();
        }

        var result = MessageReadResult.NoMessage;
        for (var attempt = 0; attempt < 20 && result.IsEmpty; attempt++)
        {
            await Task.Delay(50);
            await using var transaction = await connection.BeginTransactionAsync();
            await delayedMessageTable.MoveDueMessages(10, connection, transaction, CancellationToken.None);
            result = await queue.TryReceive(connection, transaction, 0, CancellationToken.None);
            await transaction.CommitAsync();
        }

        Assert.That(result.Message.Body.ToArray(), Is.EqualTo(body.ToArray()).AsCollection);
    }

    [SetUp]
    public async Task SetUp()
    {
        connectionFactory = new PostgreSqlDbConnectionFactory(ConfigurePostgreSqlTransportInfrastructure.ConnectionString);
        var sqlConstants = new PostgreSqlConstants();
        queue = new PostgreSqlTableBasedQueue(sqlConstants, QueueName, QueueName, true);

        var addressTranslator = new QueueAddressTranslator("public", null, new QueueSchemaOptions());
        queueAddress = addressTranslator.Parse(QueueName);
        delayedQueueAddress = addressTranslator.Parse(QueueName + "_delayed");
        var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator.Parse, false);
        await queueCreator.CreateQueueIfNecessary([QueueName], delayedQueueAddress, CancellationToken.None);

        await using var connection = await connectionFactory.OpenNewConnection(CancellationToken.None);
        await queue.Purge(connection, CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = $"DELETE FROM {delayedQueueAddress.QualifiedTableName}";
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    PostgreSqlTableBasedQueue queue;
    PostgreSqlDbConnectionFactory connectionFactory;
    CanonicalQueueAddress queueAddress;
    CanonicalQueueAddress delayedQueueAddress;

    const string QueueName = "body_slice_tests";
}
