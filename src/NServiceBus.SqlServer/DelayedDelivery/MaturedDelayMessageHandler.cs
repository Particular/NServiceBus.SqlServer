namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;

    class MaturedDelayMessageHandler
    {
        DelayedMessageTable table;
        SqlConnectionFactory connectionFactory;
        TimeSpan resolution;
        CancellationToken cancellationToken;
        CancellationTokenSource cancellationTokenSource;
        Task task;

        public MaturedDelayMessageHandler(DelayedMessageTable table, SqlConnectionFactory connectionFactory, TimeSpan resolution)
        {
            this.table = table;
            this.connectionFactory = connectionFactory;
            this.resolution = resolution;
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
                            await table.MoveMaturedMessages(connection, transaction).ConfigureAwait(false);
                            transaction.Commit();
                        }
                    }
                    Logger.DebugFormat("Scheduling next attempt to move matured delayed messages to input queue in {0}", resolution);
                    await Task.Delay(resolution, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown
                }
                catch (SqlException e) when (cancellationToken.IsCancellationRequested)
                {
                    Logger.Debug("Exception thown while performing cancellation", e);
                }
                catch (Exception e)
                {
                    Logger.Fatal("Exception thown while performing cancellation", e);
                }
            }
        }

        static ILog Logger = LogManager.GetLogger<MaturedDelayMessageHandler>();
    }
}