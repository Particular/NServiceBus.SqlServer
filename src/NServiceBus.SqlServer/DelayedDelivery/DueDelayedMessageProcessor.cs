namespace NServiceBus.Transport.SQLServer
{
    using System;
    using Microsoft.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class DueDelayedMessageProcessor
    {
        public DueDelayedMessageProcessor(DelayedMessageTable table, SqlConnectionFactory connectionFactory, TimeSpan interval, int batchSize)
        {
            this.table = table;
            this.connectionFactory = connectionFactory;
            this.interval = interval;
            this.batchSize = batchSize;
            message = $"Scheduling next attempt to move matured delayed messages to input queue in {interval}";
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
                            await table.MoveDueMessages(batchSize, connection, transaction, cancellationToken).ConfigureAwait(false);
                            transaction.Commit();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    return;
                }
                catch (SqlException e) when (cancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Exception thrown while performing cancellation", e);
                    return;
                }
                catch (Exception e)
                {
                    Logger.Fatal("Exception thrown while moving matured delayed messages", e);
                }
                finally
                {
                    Logger.DebugFormat(message);
                    await Task.Delay(interval, cancellationToken).IgnoreCancellation()
                        .ConfigureAwait(false);
                }
            }
        }

        string message;
        DelayedMessageTable table;
        SqlConnectionFactory connectionFactory;
        TimeSpan interval;
        int batchSize;
        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        Task task;

        static ILog Logger = LogManager.GetLogger<DueDelayedMessageProcessor>();
    }
}