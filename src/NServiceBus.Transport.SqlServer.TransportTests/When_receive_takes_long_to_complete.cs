namespace NServiceBus.TransportTests;

using System;
using System.Threading;
using System.Threading.Tasks;
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
        var cancellationToken = TestContext.CurrentContext.CancellationToken;

        queue = await CreateATestQueue(connectionFactory, cancellationToken);

        await SendAMessage(connectionFactory, queue, cancellationToken);
        await SendAMessage(connectionFactory, queue, cancellationToken);

        var (txStarted, txFinished, txCompletionSource) = SpawnALongRunningReceiveTransaction(connectionFactory, queue);

        await txStarted;

        int peekCount;

        using (var connection = await connectionFactory.OpenNewConnection(cancellationToken))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken);

            queue.FormatPeekCommand();
            peekCount = await queue.TryPeek(connection, transaction, null, cancellationToken);
        }

        txCompletionSource.SetResult();
        await txFinished;

        Assert.That(peekCount, Is.EqualTo(1), "A long running receive transaction should not skew the estimation for number of messages in the queue.");
    }

    static async Task<SqlTableBasedQueue> CreateATestQueue(SqlServerDbConnectionFactory connectionFactory, CancellationToken cancellationToken)
    {
        var queueName = "queue_length_estimation_test";

        var sqlConstants = new SqlServerConstants();

        var queue = new SqlTableBasedQueue(sqlConstants, queueName, queueName, false);

        var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);
        var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator.Parse, false);

        await queueCreator.CreateQueueIfNecessary(new[] { queueName }, null, cancellationToken);

        await using var connection = await connectionFactory.OpenNewConnection(cancellationToken);
        await queue.Purge(connection, cancellationToken);

        return queue;
    }

    static async Task SendAMessage(SqlServerDbConnectionFactory connectionFactory, SqlTableBasedQueue queue, CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory.OpenNewConnection(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await queue.Send(
            new OutgoingMessage(Guid.NewGuid().ToString(), [], Array.Empty<byte>()),
            TimeSpan.MaxValue, connection, transaction,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
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
        connectionFactory = new SqlServerDbConnectionFactory(ConfigureSqlServerTransportInfrastructure.ConnectionString);

        queue = await CreateATestQueue(connectionFactory, CancellationToken.None);
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

    SqlTableBasedQueue queue;
    SqlServerDbConnectionFactory connectionFactory;
}