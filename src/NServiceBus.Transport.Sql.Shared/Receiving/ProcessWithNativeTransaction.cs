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
        public override async Task<ProcessOutcome> ProcessMessage(ReceiveAttempt receiveAttempt, CancellationToken cancellationToken = default)
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
                        var receiveResult = await receiveAttempt.Receive(connection, transaction, cancellationToken).ConfigureAwait(false);

                        if (receiveResult.IsEmpty)
                        {
                            return ProcessOutcome.NoMessage;
                        }

                        if (receiveResult.IsPoison)
                        {
                            await ErrorQueue.DeadLetter(receiveResult.PoisonMessage, connection, transaction, cancellationToken).ConfigureAwait(false);
                            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                            return ProcessOutcome.Committed;
                        }

                        message = receiveResult.Message;

                        if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction, cancellationToken).ConfigureAwait(false))
                        {
                            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                            return ProcessOutcome.Committed;
                        }

                        var transportTransaction = transactionForReceiveOnly
                            ? TransportTransactions.ReceiveOnly(connection, transaction)
                            : TransportTransactions.SendsAtomicWithReceive(connection, transaction);

                        if (!await TryProcess(receiveResult.Message, transportTransaction, context, cancellationToken).ConfigureAwait(false))
                        {
                            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                            return ProcessOutcome.RolledBack;
                        }

                        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }

                failureInfoStorage.ClearFailureInfoForMessage(message.TransportId);
                return ProcessOutcome.Committed;
            }
            catch (Exception ex) when (!exceptionClassifier.IsOperationCancelled(ex, cancellationToken))
            {
                if (message == null)
                {
                    throw;
                }
                failureInfoStorage.RecordFailureInfoForMessage(message.TransportId, ex, context);
                return ProcessOutcome.RolledBack;
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