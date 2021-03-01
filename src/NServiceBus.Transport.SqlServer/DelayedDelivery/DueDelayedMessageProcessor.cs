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

        public void Start(CancellationToken startCancellationToken)
        {
            pumpCancellationTokenSource = new CancellationTokenSource();

            moveDelayedMessagesTask = Task.Run(() => MoveMaturedDelayedMessages(pumpCancellationTokenSource.Token), startCancellationToken);
        }

        public async Task Stop()
        {
            pumpCancellationTokenSource?.Cancel();

            await moveDelayedMessagesTask.ConfigureAwait(false);

            pumpCancellationTokenSource?.Dispose();
        }

        async Task MoveMaturedDelayedMessages(CancellationToken pumpCancellationToken)
        {
            while (!pumpCancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var connection = await connectionFactory.OpenNewConnection(pumpCancellationToken).ConfigureAwait(false))
                    {
                        using (var transaction = connection.BeginTransaction())
                        {
                            await table.MoveDueMessages(batchSize, connection, transaction, pumpCancellationToken).ConfigureAwait(false);
                            transaction.Commit();
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                    return;
                }
                catch (SqlException e) when (pumpCancellationToken.IsCancellationRequested)
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
                    await Task.Delay(interval, pumpCancellationToken).ConfigureAwait(false);
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
        CancellationTokenSource pumpCancellationTokenSource;
        Task moveDelayedMessagesTask;

        static ILog Logger = LogManager.GetLogger<DueDelayedMessageProcessor>();
    }
}