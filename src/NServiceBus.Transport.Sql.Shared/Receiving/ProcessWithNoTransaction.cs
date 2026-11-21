namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Data;
    using System.Threading;
    using System.Threading.Tasks;
    using Extensibility;

    class ProcessWithNoTransaction(DbConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache, IExceptionClassifier exceptionClassifier)
        : ProcessStrategy(tableBasedQueueCache, exceptionClassifier, failureInfoStorage)
    {
        public override async Task ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource,
            ReceiveCountdownEvent.Signaler receiveCountdownEventSignaler, CancellationToken cancellationToken = default)
        {
            Message message = null;
            var context = new ContextBag();

            var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
            await using var connectionDisposer = connection.ConfigureAwait(false);
            try
            {
                var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
                await using var transactionDisposer = transaction.ConfigureAwait(false);
                var receiveResult = await InputQueue.TryReceive(connection, transaction, cancellationToken).ConfigureAwait(false);
                receiveCountdownEventSignaler.Signal();

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
                    await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                message = receiveResult.Message;

                if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction,
                        cancellationToken).ConfigureAwait(false))
                {
                    await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                    return;
                }

                await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
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

        readonly FailureInfoStorage failureInfoStorage = failureInfoStorage;
        readonly IExceptionClassifier exceptionClassifier = exceptionClassifier;
    }
}