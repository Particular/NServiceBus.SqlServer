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
        public override async Task<ProcessOutcome> ProcessMessage(ReceiveAttempt receiveAttempt, CancellationToken cancellationToken = default)
        {
            Message message = null;
            var context = new ContextBag();

            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    using (var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted))
                    {
                        var receiveResult = await receiveAttempt.Receive(connection, transaction, cancellationToken)
                            .ConfigureAwait(false);

                        if (receiveResult == MessageReadResult.NoMessage)
                        {
                            return ProcessOutcome.NoMessage;
                        }

                        if (receiveResult.IsPoison)
                        {
                            await ErrorQueue
                                .DeadLetter(receiveResult.PoisonMessage, connection, transaction, cancellationToken)
                                .ConfigureAwait(false);
                            transaction.Commit();
                            return ProcessOutcome.Committed;
                        }

                        message = receiveResult.Message;

                        if (await TryHandleDelayedMessage(receiveResult.Message, connection, transaction,
                                cancellationToken).ConfigureAwait(false))
                        {
                            transaction.Commit();
                            return ProcessOutcome.Committed;
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
                    return ProcessOutcome.RolledBack;
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

            return ProcessOutcome.Committed;
        }

        readonly FailureInfoStorage failureInfoStorage = failureInfoStorage;
        readonly IExceptionClassifier exceptionClassifier = exceptionClassifier;
    }
}