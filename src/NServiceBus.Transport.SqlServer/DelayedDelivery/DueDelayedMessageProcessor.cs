namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
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

        public void Start(CancellationToken cancellationToken)
        {
            moveDelayedMessagesCancellationTokenSource = new CancellationTokenSource();

            moveDelayedMessagesTask = Task.Run(() => MoveMaturedDelayedMessages(moveDelayedMessagesCancellationTokenSource.Token), cancellationToken);
        }

        public async Task Stop()
        {
            moveDelayedMessagesCancellationTokenSource?.Cancel();

            await moveDelayedMessagesTask.ConfigureAwait(false);

            moveDelayedMessagesCancellationTokenSource?.Dispose();
        }

        async Task MoveMaturedDelayedMessages(CancellationToken moveDelayedMessagesCancellationToken)
        {
            while (!moveDelayedMessagesCancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var connection = await connectionFactory.OpenNewConnection(moveDelayedMessagesCancellationToken).ConfigureAwait(false))
                    {
                        using (var transaction = connection.BeginTransaction())
                        {
                            await table.MoveDueMessages(batchSize, connection, transaction, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                            transaction.Commit();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    return;
                }
                catch (SqlException e) when (moveDelayedMessagesCancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Exception thrown while performing cancellation", e);
                    return;
                }
                catch (Exception e)
                {
                    Logger.Fatal("Exception thrown while moving matured delayed messages", e);
                }

                try
                {
                    Logger.DebugFormat(message);
                    await Task.Delay(interval, moveDelayedMessagesCancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        string message;
        DelayedMessageTable table;
        SqlConnectionFactory connectionFactory;
        TimeSpan interval;
        int batchSize;
        CancellationTokenSource moveDelayedMessagesCancellationTokenSource;
        Task moveDelayedMessagesTask;

        static ILog Logger = LogManager.GetLogger<DueDelayedMessageProcessor>();
    }
}