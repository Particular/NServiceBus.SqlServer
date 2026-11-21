namespace NServiceBus.TransportTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Transport;
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
            var result = await queue.TryReceive(connection, transaction, CancellationToken.None);
            await transaction.CommitAsync();

            Assert.That(result.Message.Body.ToArray(), Is.EqualTo(body.ToArray()).AsCollection);
        }
    }

    [SetUp]
    public async Task SetUp()
    {
        connectionFactory = new PostgreSqlDbConnectionFactory(ConfigurePostgreSqlTransportInfrastructure.ConnectionString);
        var sqlConstants = new PostgreSqlConstants();
        queue = new PostgreSqlTableBasedQueue(sqlConstants, QueueName, QueueName, true);

        var addressTranslator = new QueueAddressTranslator("public", null, new QueueSchemaOptions());
        var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator.Parse, false);
        await queueCreator.CreateQueueIfNecessary([QueueName], null, CancellationToken.None);

        await using var connection = await connectionFactory.OpenNewConnection(CancellationToken.None);
        await queue.Purge(connection, CancellationToken.None);
    }

    PostgreSqlTableBasedQueue queue;
    PostgreSqlDbConnectionFactory connectionFactory;

    const string QueueName = "body_slice_tests";
}
