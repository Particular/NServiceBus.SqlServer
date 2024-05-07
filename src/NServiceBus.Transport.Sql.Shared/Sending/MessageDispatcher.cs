namespace NServiceBus.Transport.Sql.Shared.Sending
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Transport;
    using Configuration;
    using DelayedDelivery;
    using Queuing;

    public class MessageDispatcher : IMessageDispatcher
    {
        public MessageDispatcher(Func<string, string> getCanonicalAddressForm, IMulticastToUnicastConverter multicastToUnicastConverter, TableBasedQueueCache tableBasedQueueCache, IDelayedMessageStore delayedMessageTable, DbConnectionFactory connectionFactory)
        {
            this.getCanonicalAddressForm = getCanonicalAddressForm;
            this.multicastToUnicastConverter = multicastToUnicastConverter;
            this.tableBasedQueueCache = tableBasedQueueCache;
            this.delayedMessageTable = delayedMessageTable;
            this.connectionFactory = connectionFactory;
        }

        // We need to check if we can support cancellation in here as well?
        public async Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, CancellationToken cancellationToken = default)
        {
            var sortedOperations = operations.UnicastTransportOperations
                .Concat(await ConvertToUnicastOperations(operations, cancellationToken).ConfigureAwait(false))
                .SortAndDeduplicate(getCanonicalAddressForm);

            if (sortedOperations.DefaultDispatch != null)
            {
                await DispatchDefault(sortedOperations.DefaultDispatch, transportTransaction, cancellationToken).ConfigureAwait(false);
            }

            if (sortedOperations.IsolatedDispatch != null)
            {
                await DispatchIsolated(sortedOperations.IsolatedDispatch, transportTransaction, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<IEnumerable<UnicastTransportOperation>> ConvertToUnicastOperations(TransportOperations operations, CancellationToken cancellationToken)
        {
            if (operations.MulticastTransportOperations.Count == 0)
            {
                return _emptyUnicastTransportOperationsList;
            }

            var tasks = operations.MulticastTransportOperations.Select(operation => multicastToUnicastConverter.Convert(operation, cancellationToken));
            var result = await Task.WhenAll(tasks).ConfigureAwait(false);
            return result.SelectMany(x => x);
        }

        async Task DispatchIsolated(IEnumerable<UnicastTransportOperation> operations, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            if (transportTransaction.IsUserProvided(out DbConnection connection, out var transaction))
            {
                await Dispatch(operations, connection, transaction, cancellationToken).ConfigureAwait(false);
                return;
            }

            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            using (connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            using (var tx = connection.BeginTransaction())
            {
                await Dispatch(operations, connection, tx, cancellationToken).ConfigureAwait(false);
                tx.Commit();
                scope.Complete();
            }
        }

        async Task DispatchDefault(IEnumerable<UnicastTransportOperation> operations, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            DbConnection connection;

            if (transportTransaction.OutsideOfHandler())
            {
                using (connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                    await Dispatch(operations, connection, null, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (transportTransaction.IsNoTransaction(out connection))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    await Dispatch(operations, connection, transaction, cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                }
            }
            else if (transportTransaction.IsReceiveOnly())
            {
                using (connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                using (var transaction = connection.BeginTransaction())
                {
                    await Dispatch(operations, connection, transaction, cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                }
            }
            else if (transportTransaction.IsSendsAtomicWithReceive(out connection, out var transaction))
            {
                await Dispatch(operations, connection, transaction, cancellationToken).ConfigureAwait(false);
            }
            else if (transportTransaction.IsTransactionScope())
            {
                using (connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                {
                    await Dispatch(operations, connection, null, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                throw new Exception("TransportTransaction is in invalid state.");
            }
        }

        async Task Dispatch(IEnumerable<UnicastTransportOperation> operations, DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken)
        {
            foreach (var operation in operations)
            {
                await Dispatch(connection, transaction, operation, cancellationToken).ConfigureAwait(false);
            }
        }

        Task Dispatch(DbConnection connection, DbTransaction transaction, UnicastTransportOperation operation, CancellationToken cancellationToken)
        {
            var discardIfNotReceivedBefore = operation.Properties.DiscardIfNotReceivedBefore;
            var doNotDeliverBefore = operation.Properties.DoNotDeliverBefore;

            if (doNotDeliverBefore != null)
            {
                if (discardIfNotReceivedBefore != null && discardIfNotReceivedBefore.MaxTime < TimeSpan.MaxValue)
                {
                    throw new Exception("Delayed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to delay messages of this type.");
                }

                return delayedMessageTable.Store(operation.Message, doNotDeliverBefore.At - DateTimeOffset.UtcNow, operation.Destination, connection, transaction, cancellationToken);
            }

            var delayDeliveryWith = operation.Properties.DelayDeliveryWith;
            if (delayDeliveryWith != null)
            {
                if (discardIfNotReceivedBefore != null && discardIfNotReceivedBefore.MaxTime < TimeSpan.MaxValue)
                {
                    throw new Exception("Delayed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to delay messages of this type.");
                }

                return delayedMessageTable.Store(operation.Message, delayDeliveryWith.Delay, operation.Destination, connection, transaction, cancellationToken);
            }

            var queue = tableBasedQueueCache.Get(operation.Destination);
            return queue.Send(operation.Message, discardIfNotReceivedBefore?.MaxTime ?? TimeSpan.MaxValue, connection, transaction, cancellationToken);
        }

        TableBasedQueueCache tableBasedQueueCache;
        IDelayedMessageStore delayedMessageTable;
        DbConnectionFactory connectionFactory;
        Func<string, string> getCanonicalAddressForm;
        IMulticastToUnicastConverter multicastToUnicastConverter;
        static UnicastTransportOperation[] _emptyUnicastTransportOperationsList = new UnicastTransportOperation[0];
    }
}