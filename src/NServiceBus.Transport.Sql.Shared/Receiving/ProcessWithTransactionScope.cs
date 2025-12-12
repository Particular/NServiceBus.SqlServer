namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;

    class ProcessWithTransactionScope(TransactionOptions transactionOptions, DbConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache, IExceptionClassifier exceptionClassifier)
        : ProcessStrategy(tableBasedQueueCache, exceptionClassifier, failureInfoStorage)
    {
        public override async Task ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource,
            ReceiveCountdownEvent.Signaler receiveCountdownEventSignaler, CancellationToken cancellationToken = default)
        {
            Message message = null;
            var context = new ContextBag();

            try
            {
                using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, transactionOptions, TransactionScopeAsyncFlowOption.Enabled))
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                    var receiveResult = await InputQueue.TryReceive(connection, null, cancellationToken).ConfigureAwait(false);
                    receiveCountdownEventSignaler.Signal();

                    if (receiveResult == MessageReadResult.NoMessage)
                    {
                        stopBatchCancellationTokenSource.Cancel();
                        return;
                    }

                    if (receiveResult.IsPoison)
                    {
                        await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, connection, null, cancellationToken).ConfigureAwait(false);
                        scope.Complete();
                        return;
                    }

                    message = receiveResult.Message;

                    if (await TryHandleDelayedMessage(receiveResult.Message, connection, null, cancellationToken).ConfigureAwait(false))
                    {
                        scope.Complete();
                        return;
                    }

                    connection.Close();

                    if (!await TryProcess(receiveResult.Message, TransportTransactions.TransactionScope(Transaction.Current), context, cancellationToken).ConfigureAwait(false))
                    {
                        return;
                    }

                    scope.Complete();
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

        FailureInfoStorage failureInfoStorage = failureInfoStorage;
        readonly IExceptionClassifier exceptionClassifier = exceptionClassifier;
    }
}