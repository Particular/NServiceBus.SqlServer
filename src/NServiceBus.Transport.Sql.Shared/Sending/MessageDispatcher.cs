namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Transactions;
    using Transport;
    using static SqlTransportTransactionState;

    class MessageDispatcher : IMessageDispatcher
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
            if (transportTransaction.State is UserProvided userProvided)
            {
                await Dispatch(operations, userProvided.Connection, userProvided.NativeTransaction, cancellationToken).ConfigureAwait(false);
                return;
            }

            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            using (var transaction = connection.BeginTransaction())
            {
                await Dispatch(operations, connection, transaction, cancellationToken).ConfigureAwait(false);
                transaction.Commit();
                scope.Complete();
            }
        }

        async Task DispatchDefault(IEnumerable<UnicastTransportOperation> operations, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            var state = transportTransaction.State;

            switch (state)
            {
                // There is no receive transaction the sends could take part in, either because dispatch
                // happens outside the message processing pipeline or because the receive transaction must
                // not be used for sends. Dispatch on a dedicated connection with its own transaction.
                case OutsideHandler:
                case ReceiveOnly:
                    {
                        using var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);
                        using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                        await Dispatch(operations, connection, transaction, cancellationToken).ConfigureAwait(false);
                        transaction.Commit();
                        break;
                    }

                // The receive connection can be reused but there is no receive transaction, so the sends
                // get their own short-lived transaction.
                case NoTransaction noTransaction:
                    {
                        using var transaction = noTransaction.Connection.BeginTransaction();

                        await Dispatch(operations, noTransaction.Connection, transaction, cancellationToken).ConfigureAwait(false);
                        transaction.Commit();
                        break;
                    }

                // The sends take part in the receive transaction or in the transaction provided by the user.
                case SendsAtomicWithReceive sendsAtomicWithReceive:
                    {
                        await Dispatch(operations, sendsAtomicWithReceive.Connection, sendsAtomicWithReceive.NativeTransaction, cancellationToken).ConfigureAwait(false);
                        break;
                    }

                case UserProvided userProvided:
                    {
                        await Dispatch(operations, userProvided.Connection, userProvided.NativeTransaction, cancellationToken).ConfigureAwait(false);
                        break;
                    }

                // The ambient transaction covers both the receive and the sends; a new connection enlists
                // in it automatically.
                case AmbientTransaction:
                    {
                        using var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false);

                        await Dispatch(operations, connection, null, cancellationToken).ConfigureAwait(false);
                        break;
                    }

                default:
                    throw new Exception($"Unsupported transport transaction state: {state.GetType().Name}.");
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