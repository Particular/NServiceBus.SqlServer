namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class DelayedMessageHandler
    {
        public DelayedMessageHandler(DelayedMessageTable table, SqlConnectionFactory connectionFactory, TimeSpan interval, int batchSize)
        {
            this.table = table;
            this.connectionFactory = connectionFactory;
            this.interval = interval;
            this.batchSize = batchSize;
        }

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;

            task = Task.Run(MoveMaturedDelayedMessages, CancellationToken.None);
        }

        public async Task Stop()
        {
            cancellationTokenSource.Cancel();

            await task.ConfigureAwait(false);

            cancellationTokenSource.Dispose();
        }

        async Task MoveMaturedDelayedMessages()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                    {
                        using (var transaction = connection.BeginTransaction())
                        {
                            await table.MoveMaturedMessages(batchSize, connection, transaction).ConfigureAwait(false);
                            transaction.Commit();
                        }
                    }
                    Logger.DebugFormat("Scheduling next attempt to move matured delayed messages to input queue in {0}", interval);
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                }
                catch (Exception e) when (cancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Exception thrown while performing cancellation", e);
                }
                catch (Exception e)
                {
                    Logger.Fatal("Exception thrown while moving matured delayed messages", e);

                    Logger.DebugFormat("Scheduling next attempt to move matured delayed messages to input queue in {0}", interval);
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        DelayedMessageTable table;
        SqlConnectionFactory connectionFactory;
        TimeSpan interval;
        int batchSize;
        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        Task task;

        static ILog Logger = LogManager.GetLogger<DelayedMessageHandler>();
    }
}
