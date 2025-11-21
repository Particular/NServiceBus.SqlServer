namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Extensibility;

    using IsolationLevel = System.Data.IsolationLevel;

    class ProcessWithNativeTransaction : ProcessStrategy
    {
        public ProcessWithNativeTransaction(TransactionOptions transactionOptions, DbConnectionFactory connectionFactory, FailureInfoStorage failureInfoStorage, TableBasedQueueCache tableBasedQueueCache, IExceptionClassifier exceptionClassifier, bool transactionForReceiveOnly = false)
        : base(tableBasedQueueCache, exceptionClassifier, failureInfoStorage)
        {
            this.connectionFactory = connectionFactory;
            this.failureInfoStorage = failureInfoStorage;
            this.exceptionClassifier = exceptionClassifier;
            this.transactionForReceiveOnly = transactionForReceiveOnly;

            isolationLevel = IsolationLevelMapper.Map(transactionOptions.IsolationLevel);
        }

        public override ProcessResult ProcessMessage(CancellationTokenSource stopBatchCancellationTokenSource, CancellationToken cancellationToken = default)
        {
            var popTaskCompletionSource = new TaskCompletionSource<bool>();

            var processingTask = ProcessingTask(popTaskCompletionSource, stopBatchCancellationTokenSource, cancellationToken);
            return new(processingTask, popTaskCompletionSource.Task);
        }

        async Task ProcessingTask(TaskCompletionSource<bool> receiveTaskCompletionSource, CancellationTokenSource stopBatchCancellationTokenSource, CancellationToken cancellationToken)
        {
            Message message = null;
            var context = new ContextBag();

            try
            {
                using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction(isolationLevel))
                {
                    var receiveResult = await InputQueue.TryReceive(connection, transaction, cancellationToken).ConfigureAwait(false);

                    // Signal that receive is complete
                    receiveTaskCompletionSource.TrySetResult(true);

                    if (receiveResult == MessageReadResult.NoMessage)
                    {
                        stopBatchCancellationTokenSource.Cancel();
                        return;
                    }

                    if (receiveResult.IsPoison)
                    {
                        await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, connection, transaction, cancellationToken).ConfigureAwait(false);
                        transaction.Commit();
                        return;
                    }

                    message = receiveResult.Message;

                    if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction, cancellationToken).ConfigureAwait(false))
                    {
                        transaction.Commit();
                        return;
                    }

                    var transportTransaction = transactionForReceiveOnly
                        ? TransportTransactions.ReceiveOnly(connection, transaction)
                        : TransportTransactions.SendsAtomicWithReceive(connection, transaction);

                    if (!await TryProcess(receiveResult.Message, transportTransaction, context, cancellationToken).ConfigureAwait(false))
                    {
                        transaction.Rollback();
                        return;
                    }

                    transaction.Commit();
                }

                failureInfoStorage.ClearFailureInfoForMessage(message.TransportId);
            }
            catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                receiveTaskCompletionSource.TrySetException(ex);

                if (message == null)
                {
                    throw;
                }
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, ex, context);
            }
            catch (Exception ex) when (exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                receiveTaskCompletionSource.TrySetCanceled(cancellationToken);
                throw;
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

        IsolationLevel isolationLevel;
        DbConnectionFactory connectionFactory;
        FailureInfoStorage failureInfoStorage;
        readonly IExceptionClassifier exceptionClassifier;
        bool transactionForReceiveOnly;
        internal static string ReceiveOnlyTransactionMode = "SqlTransport.ReceiveOnlyTransactionMode";
    }
}