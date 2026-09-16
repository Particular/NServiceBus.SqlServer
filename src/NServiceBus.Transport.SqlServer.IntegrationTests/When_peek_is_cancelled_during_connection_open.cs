namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using IsolationLevel = System.Transactions.IsolationLevel;

    // QueuePeeker.Peek opens a connection, with the receive cancellation token, inside an ambient
    // TransactionScope. MessageReceiver.StopReceive cancels that token, so every endpoint shutdown
    // cancels an in-flight open. Against a Pooling=False connection string that can deadlock inside
    // Microsoft.Data.SqlClient:
    //
    //   thread A  SqlConnectionFactory.CreateReplaceConnectionContinuation keeps completing the login
    //             the caller has already given up on. CompleteLogin -> EnlistNonNull registers the
    //             promotable single phase enlistment and then assigns EnlistedTransaction. The
    //             transaction has already aborted by then, so add_TransactionCompleted invokes the
    //             handler inline and DetachTransaction blocks on lock(connection).
    //
    //   thread B  the cancelled OpenAsync unwinds Peek, TransactionScope.Dispose rolls the
    //             transaction back, and SqlDelegatedTransaction.Rollback takes lock(connection) and
    //             then blocks on the parser lock thread A holds for the duration of the login.
    //
    // Nothing breaks the cycle. The peek loop never completes, so MessageReceiver.StopReceive waits
    // on messageReceivingTask forever and the endpoint never shuts down.
    //
    // Reproduces on the pinned Microsoft.Data.SqlClient 6.1.6 and on 7.0.3, typically inside a
    // minute. It was first seen in the wild against LocalDb, which hands out Pooling=False
    // connection strings for test databases.
    //
    // Tracked upstream as https://github.com/dotnet/SqlClient/issues/4696. This test should start
    // passing without the QueuePeeker workaround once that is fixed.
    [Explicit("Stress test. When it passes it just burns CPU for a couple of minutes; when it fails it deliberately leaves deadlocked threads and a wedged SQL connection behind, so it must not run as part of the normal suite.")]
    public class When_peek_is_cancelled_during_connection_open
    {
        [Test]
        public async Task Peek_does_not_deadlock()
        {
            await Prepare();

            var openMillis = await MeasureOpen();
            TestContext.Out.WriteLine($"open takes ~{openMillis:F2} ms; cancelling at 0.55..1.25x that");

            var deadline = Stopwatch.GetTimestamp() + (long)(RunFor.TotalSeconds * Stopwatch.Frequency);
            var progress = new long[Workers];
            var iterations = new long[Workers];
            for (var i = 0; i < Workers; i++)
            {
                progress[i] = Stopwatch.GetTimestamp();
            }

            var state = new RunState(deadline, progress, iterations, openMillis);
            for (var i = 0; i < Workers; i++)
            {
                var worker = i;

                // Deliberately not tracked: on a deadlock these never complete, and awaiting them
                // would turn a failing test into a hanging one.
                _ = Task.Run(() => Worker(worker, state), CancellationToken.None);
            }

            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);

                var now = Stopwatch.GetTimestamp();
                var total = 0L;
                var stalled = -1;
                var worstStall = 0d;

                for (var i = 0; i < Workers; i++)
                {
                    total += Volatile.Read(ref iterations[i]);

                    var stall = (now - Volatile.Read(ref progress[i])) / (double)Stopwatch.Frequency;
                    if (stall > worstStall)
                    {
                        worstStall = stall;
                        if (stall > StallTimeout.TotalSeconds)
                        {
                            stalled = i;
                        }
                    }
                }

                if (stalled >= 0)
                {
                    Assert.Fail(
                        $"Peek deadlocked: worker {stalled} made no progress for {worstStall:F0}s, after " +
                        $"{total} peeks. Take a dump of this process: syncblk shows a thread in " +
                        "SqlDelegatedTransaction.Rollback owning the connection's monitor, and another in " +
                        "DbConnectionInternal.CleanupConnectionOnTransactionCompletion waiting on it while " +
                        "holding the parser lock and the InternalTransaction.");
                }

                if (now > deadline)
                {
                    TestContext.Out.WriteLine($"{total} peeks cancelled mid-open without deadlocking.");
                    return;
                }
            }
        }

        async Task Worker(int worker, RunState state, CancellationToken cancellationToken = default)
        {
            var random = new Random(worker * 7919);

            while (Stopwatch.GetTimestamp() < state.Deadline)
            {
                var count = Volatile.Read(ref state.Iterations[worker]);

                if (count % 8 == 7)
                {
                    // Re-measure, so the cancellation stays aimed at the login however the machine
                    // happens to be behaving.
                    state.RecordOpen(await MeasureOpenOnce(cancellationToken));
                }
                else
                {
                    using var source = new CancellationTokenSource();

                    // Peek runs synchronously as far as the OpenAsync inside OpenNewConnection, so by
                    // the time it hands back a task the scope exists and the login is in flight.
                    var peek = peeker.Peek(queue, circuitBreaker, source.Token);

                    // CancelAfter rides the OS timer, which is an order of magnitude coarser than a
                    // login. Spinning is the only way to land inside the window.
                    SpinFor(state.OpenMillis * (0.55 + (random.NextDouble() * 0.7)));
                    await source.CancelAsync();

                    try
                    {
                        await peek;
                    }
#pragma warning disable PS0019 // Do not catch Exception without considering OperationCanceledException
                    catch (Exception)
#pragma warning restore PS0019
                    {
                        // Cancelling the peek is the point of the test.
                    }
                }

                Volatile.Write(ref state.Progress[worker], Stopwatch.GetTimestamp());
                Interlocked.Increment(ref state.Iterations[worker]);
            }
        }

        async Task<double> MeasureOpen(CancellationToken cancellationToken = default)
        {
            var samples = new List<double>();
            for (var i = 0; i < 10; i++)
            {
                samples.Add(await MeasureOpenOnce(cancellationToken));
            }

            samples.Sort();
            return samples[samples.Count / 2];
        }

        // Timed inside a scope on purpose: the window this test aims at is the promotable single
        // phase enlistment, which is a round trip at the very end of the login.
        async Task<double> MeasureOpenOnce(CancellationToken cancellationToken = default)
        {
            var start = Stopwatch.GetTimestamp();

            using (var scope = new TransactionScope(
                       TransactionScopeOption.RequiresNew,
                       new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                       TransactionScopeAsyncFlowOption.Enabled))
            using (await dbConnectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                scope.Complete();
            }

            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        static void SpinFor(double milliseconds)
        {
            var deadline = Stopwatch.GetTimestamp() + (long)(milliseconds * Stopwatch.Frequency / 1000d);
            while (Stopwatch.GetTimestamp() < deadline)
            {
                Thread.SpinWait(40);
            }
        }

        async Task Prepare(CancellationToken cancellationToken = default)
        {
            var configured = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ??
                             @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            // Pooling=False is the precondition. It is what sends OpenAsync down
            // SqlConnectionFactory's s_pendingOpenNonPooled path, where the login continues on a
            // thread pool thread after the caller's open has already been cancelled. Test databases
            // handed out by LocalDb are non pooled, which is where this was first seen.
            var connectionString = new SqlConnectionStringBuilder(configured) { Pooling = false }.ConnectionString;

            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, new QueueSchemaAndCatalogOptions());
            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);
            await queueCreator.CreateQueueIfNecessary(
                [QueueName],
                new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"),
                cancellationToken);

            var queueAddress = addressTranslator.Parse(QueueName);
            queue = new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, true);

            peeker = new QueuePeeker(dbConnectionFactory, new SqlServerExceptionClassifier(), TimeSpan.Zero);
            circuitBreaker = new RepeatedFailuresOverTimeCircuitBreaker(
                "peek-deadlock-repro",
                TimeSpan.FromMinutes(10),
                _ => { });
        }

        sealed class RunState(long deadline, long[] progress, long[] iterations, double openMillis)
        {
            public long Deadline { get; } = deadline;

            public long[] Progress { get; } = progress;

            public long[] Iterations { get; } = iterations;

            public double OpenMillis => Volatile.Read(ref currentOpenMillis);

            public void RecordOpen(double sample) =>
                Volatile.Write(ref currentOpenMillis, (Volatile.Read(ref currentOpenMillis) * 0.9) + (sample * 0.1));

            double currentOpenMillis = openMillis;
        }

        SqlServerDbConnectionFactory dbConnectionFactory;
        TableBasedQueue queue;
        QueuePeeker peeker;
        RepeatedFailuresOverTimeCircuitBreaker circuitBreaker;

        readonly SqlServerConstants sqlConstants = new();

        const string QueueName = "PeekDeadlockRepro";
        const int Workers = 8;

        static readonly TimeSpan RunFor = TimeSpan.FromMinutes(2);
        static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(20);
    }
}
