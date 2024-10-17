#pragma warning disable PS0018
namespace NServiceBus.TransportTests;

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Transport;
using Transport.SqlServer;

//HINT: This test operates on the lower level than the transport tests because we need to verify
//      internal behavior of the transport. In this case the peek behavior that is not exposed by the transport seam.
//      Therefore we are not using the transport base class.
public class When_receive_takes_long_to_complete
{
    [TestCase(TransportTransactionMode.None)]
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    [TestCase(TransportTransactionMode.TransactionScope)]
    public async Task Peeker_should_provide_accurate_queue_length_estimate(TransportTransactionMode transactionMode)
    {
        queue = await CreateATestQueue(connectionFactory);

        await SendAMessage(connectionFactory, queue);
        await SendAMessage(connectionFactory, queue);

        var (txStarted, txFinished, txCompletionSource) = SpawnALongRunningReceiveTransaction(connectionFactory, queue);

        await txStarted;

        int peekCount;

        using (var connection = await connectionFactory.OpenNewConnection())
        {
            var transaction = await connection.BeginTransactionAsync();

            queue.FormatPeekCommand();
            peekCount = await queue.TryPeek(connection, transaction, null);
        }

        txCompletionSource.SetResult();
        await txFinished;

        Assert.That(peekCount, Is.EqualTo(1), "A long running receive transaction should not skew the estimation for number of messages in the queue.");
    }

    async Task<SqlTableBasedQueue> CreateATestQueue(SqlServerDbConnectionFactory connectionFactory)
    {
        var queueName = "queue_length_estimation_test";

        var sqlConstants = new SqlServerConstants();

        var queue = new SqlTableBasedQueue(sqlConstants, new CanonicalQueueAddress(queueName, "dbo", catalogName), queueName, false);

        var addressTranslator = new QueueAddressTranslator(catalogName, "dbo", null, null);
        var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator.Parse, false);

        await queueCreator.CreateQueueIfNecessary(new[] { queueName }, null);

        await using var connection = await connectionFactory.OpenNewConnection();
        await queue.Purge(connection);

        return queue;
    }

    static async Task SendAMessage(SqlServerDbConnectionFactory connectionFactory, SqlTableBasedQueue queue)
    {
        await using var connection = await connectionFactory.OpenNewConnection();
        var transaction = await connection.BeginTransactionAsync();

        await queue.Send(
            new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>()),
            TimeSpan.MaxValue, connection, transaction);

        await transaction.CommitAsync();
    }

    (Task, Task, TaskCompletionSource) SpawnALongRunningReceiveTransaction(SqlServerDbConnectionFactory connectionFactory, SqlTableBasedQueue queue)
    {
        var started = new TaskCompletionSource();
        var cancellationTokenSource = new TaskCompletionSource();

        var task = Task.Run(async () =>
        {
            await using var connection = await connectionFactory.OpenNewConnection();
            var transaction = await connection.BeginTransactionAsync();

            await queue.TryReceive(connection, transaction);

            started.SetResult();

            await cancellationTokenSource.Task;
        });

        return (started.Task, task, cancellationTokenSource);
    }

    [SetUp]
    public async Task Setup()
    {
        var connectionString = ConfigureSqlServerTransportInfrastructure.ConnectionString;
        var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

        catalogName = connectionStringBuilder.InitialCatalog;

        connectionFactory = new SqlServerDbConnectionFactory(ConfigureSqlServerTransportInfrastructure.ConnectionString);

        queue = await CreateATestQueue(connectionFactory);
    }

    [TearDown]
    public async Task TearDown()
    {
        if (queue == null)
        {
            return;
        }

        await using var connection = await connectionFactory.OpenNewConnection(CancellationToken.None);
        await using var comm = connection.CreateCommand();

        comm.CommandText = $"IF OBJECT_ID('{queue}', 'U') IS NOT NULL DROP TABLE {queue}";

        await comm.ExecuteNonQueryAsync(CancellationToken.None);
    }

    string catalogName;
    SqlTableBasedQueue queue;
    SqlServerDbConnectionFactory connectionFactory;
}