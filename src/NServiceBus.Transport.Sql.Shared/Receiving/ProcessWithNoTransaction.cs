namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;

    class ProcessWithNoTransaction : ProcessStrategy
    {
        public ProcessWithNoTransaction(DbConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache, IExceptionClassifier exceptionClassifier)
        : base(tableBasedQueueCache, exceptionClassifier, failureInfoStorage)
        {
            this.connectionFactory = connectionFactory;
            this.failureInfoStorage = failureInfoStorage;
            this.exceptionClassifier = exceptionClassifier;
        }

        public override async Task ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource, CancellationToken cancellationToken = default)
        {
            Message message = null;
            var context = new ContextBag();

            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        var receiveResult = await InputQueue.TryReceive(connection, transaction, cancellationToken)
                                .ConfigureAwait(false);

                        if (receiveResult == MessageReadResult.NoMessage)
                        {
                            stopBatchCancellationTokenSource.Cancel();
                            return;
                        }

                        if (receiveResult.IsPoison)
                        {
                            await ErrorQueue
                                .DeadLetter(receiveResult.PoisonMessage, connection, transaction, cancellationToken)
                                .ConfigureAwait(false);
                            transaction.Commit();
                            return;
                        }

                        message = receiveResult.Message;

                        if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction,
                                cancellationToken).ConfigureAwait(false))
                        {
                            transaction.Commit();
                            return;
                        }

                        transaction.Commit();
                    }
                }
                catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
                {
                    if (message == null)
                    {
                        throw;
                    }
                    failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, ex, context);
                    return;
                }

                var transportTransaction = TransportTransactions.NoTransaction(connection);

                try
                {
                    await TryHandleMessage(message, transportTransaction, context, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
                {
                    // Since this is TransactionMode.None, we don't care whether error handling says handled or retry. Message is gone either way.
                    _ = await HandleError(ex, message, transportTransaction, 1, context, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        readonly DbConnectionFactory connectionFactory;
        readonly FailureInfoStorage failureInfoStorage;
        readonly IExceptionClassifier exceptionClassifier;
    }
}