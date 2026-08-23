namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;

    using IsolationLevel = System.Data.IsolationLevel;

    class ProcessWithNativeTransaction(TransactionOptions transactionOptions, DbConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache, IExceptionClassifier exceptionClassifier, bool transactionForReceiveOnly = false)
        : ProcessStrategy(tableBasedQueueCache, exceptionClassifier, failureInfoStorage)
    {
        public override async Task ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource,
            ReceiveCountdownEvent.Signaler receiveCountdownEventSignaler, CancellationToken cancellationToken = default)
        {
            Message message = null;
            var context = new ContextBag();

            try
            {
                var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
                await using (connection.ConfigureAwait(false))
                {
                    var transaction = await connection.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
                    await using (transaction.ConfigureAwait(false))
                    {
                        var receiveResult = await TryReceiveAnchored(connection, transaction, cancellationToken).ConfigureAwait(false);
                        receiveCountdownEventSignaler.Signal();

                        if (receiveResult.IsEmpty)
                        {
                            await stopBatchCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                            return;
                        }

                        if (receiveResult.IsPoison)
                        {
                            await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, connection, transaction, cancellationToken).ConfigureAwait(false);
                            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                            Anchor.Advance(receiveResult.RowVersion);
                            return;
                        }

                        message = receiveResult.Message;

                        if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction, cancellationToken).ConfigureAwait(false))
                        {
                            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                            Anchor.Advance(receiveResult.RowVersion);
                            return;
                        }

                        var transportTransaction = transactionForReceiveOnly
                            ? TransportTransactions.ReceiveOnly(connection, transaction)
                            : TransportTransactions.SendsAtomicWithReceive(connection, transaction);

                        if (!await TryProcess(receiveResult.Message, transportTransaction, context, cancellationToken).ConfigureAwait(false))
                        {
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                            // the message is visible at the head of the queue again; rescan from the
                            // head so the immediate retry finds it
                            Anchor.Reset();
                            return;
                        }

                        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                        Anchor.Advance(receiveResult.RowVersion);
                    }
                }

                failureInfoStorage.ClearFailureInfoForMessage(message.TransportId);
            }
            catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                if (message == null)
                {
                    throw;
                }
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, ex, context);
                // the receive transaction rolled back and the message is visible again
                Anchor.Reset();
            }
        }

        async Task<bool> TryProcess(Message message, TransportTransaction transportTransaction, ContextBag context, CancellationToken cancellationToken)
        {
            if (failureInfoStorage.TryGetFailureInfoForMessage(message.TransportId, out var failure))
            {
                var errorHandlingResult = await HandleError(failure.Exception, message, transportTransaction, failure.NumberOfProcessingAttempts, failure.Context, cancellationToken).ConfigureAwait(false);

                if (errorHandlingResult == ErrorHandleResult.Handled)
                {
                    return true;
                }
            }

            try
            {
                return await TryHandleMessage(message, transportTransaction, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, ex, context);
                return false;
            }
        }

        IsolationLevel isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        FailureInfoStorage failureInfoStorage = failureInfoStorage;
        readonly IExceptionClassifier exceptionClassifier = exceptionClassifier;
    }
}