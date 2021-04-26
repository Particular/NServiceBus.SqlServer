namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Collections.Generic;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using Transport;
    using System.Threading;

    class MessageDispatcher : IMessageDispatcher
    {
        public MessageDispatcher(QueueAddressTranslator addressTranslator, IMulticastToUnicastConverter multicastToUnicastConverter, TableBasedQueueCache tableBasedQueueCache, IDelayedMessageStore delayedMessageTable, SqlConnectionFactory connectionFactory)
        {
            this.addressTranslator = addressTranslator;
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
                .SortAndDeduplicate(addressTranslator);

            await DispatchDefault(sortedOperations, transportTransaction, cancellationToken).ConfigureAwait(false);
            await DispatchIsolated(sortedOperations, transportTransaction, cancellationToken).ConfigureAwait(false);
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

        async Task DispatchIsolated(SortingResult sortedOperations, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            if (sortedOperations.IsolatedDispatch == null)
            {
                return;
            }

            transportTransaction.TryGet(SettingsKeys.IsUserProvidedTransactionKey, out bool userProvidedTransaction);

            if (userProvidedTransaction)
            {
                transportTransaction.TryGet(SettingsKeys.TransportTransactionSqlTransactionKey, out SqlTransaction sqlTransportTransaction);
                if (sqlTransportTransaction != null)
                {
                    await Dispatch(sortedOperations.IsolatedDispatch, sqlTransportTransaction.Connection, sqlTransportTransaction, cancellationToken).ConfigureAwait(false);
                    return;
                }

                transportTransaction.TryGet(SettingsKeys.TransportTransactionSqlConnectionKey, out SqlConnection sqlTransportConnection);
                if (sqlTransportConnection != null)
                {
                    await Dispatch(sortedOperations.IsolatedDispatch, sqlTransportConnection, null, cancellationToken).ConfigureAwait(false);
                    return;
                }

                throw new Exception($"Invalid {nameof(TransportTransaction)} state. Transaction provided by the user but contains no SqlTransaction or SqlConnection objects.");
            }

            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            using (var tx = connection.BeginTransaction())
            {
                await Dispatch(sortedOperations.IsolatedDispatch, connection, tx, cancellationToken).ConfigureAwait(false);
                tx.Commit();
                scope.Complete();
            }
        }

        async Task DispatchDefault(SortingResult sortedOperations, TransportTransaction transportTransaction, CancellationToken cancellationToken)
        {
            if (sortedOperations.DefaultDispatch == null)
            {
                return;
            }

            if (InReceiveWithNoTransactionMode(transportTransaction) || InReceiveOnlyTransportTransactionMode(transportTransaction))
            {
                await DispatchUsingNewConnectionAndTransaction(sortedOperations.DefaultDispatch, cancellationToken).ConfigureAwait(false);
                return;
            }

            await DispatchUsingReceiveTransaction(transportTransaction, sortedOperations.DefaultDispatch, cancellationToken).ConfigureAwait(false);
        }


        async Task DispatchUsingNewConnectionAndTransaction(IEnumerable<UnicastTransportOperation> operations, CancellationToken cancellationToken)
        {
            using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    await Dispatch(operations, connection, transaction, cancellationToken).ConfigureAwait(false);
                    transaction.Commit();
                }
            }
        }

        async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, IEnumerable<UnicastTransportOperation> operations, CancellationToken cancellationToken)
        {
            transportTransaction.TryGet(SettingsKeys.TransportTransactionSqlConnectionKey, out SqlConnection sqlTransportConnection);
            transportTransaction.TryGet(SettingsKeys.TransportTransactionSqlTransactionKey, out SqlTransaction sqlTransportTransaction);
            transportTransaction.TryGet(out Transaction ambientTransaction);

            if (ambientTransaction != null)
            {
                if (sqlTransportConnection == null)
                {
                    using (var connection = await connectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
                    {
                        await Dispatch(operations, connection, null, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await Dispatch(operations, sqlTransportConnection, null, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                await Dispatch(operations, sqlTransportConnection, sqlTransportTransaction, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task Dispatch(IEnumerable<UnicastTransportOperation> operations, SqlConnection connection, SqlTransaction transaction, CancellationToken cancellationToken)
        {
            foreach (var operation in operations)
            {
                await Dispatch(connection, transaction, operation, cancellationToken).ConfigureAwait(false);
            }
        }

        Task Dispatch(SqlConnection connection, SqlTransaction transaction, UnicastTransportOperation operation, CancellationToken cancellationToken)
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

        static bool InReceiveWithNoTransactionMode(TransportTransaction transportTransaction)
        {
            transportTransaction.TryGet(SettingsKeys.TransportTransactionSqlTransactionKey, out SqlTransaction nativeTransaction);
            transportTransaction.TryGet(out Transaction ambientTransaction);

            return nativeTransaction == null && ambientTransaction == null;
        }

        static bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            return transportTransaction.TryGet(ProcessWithNativeTransaction.ReceiveOnlyTransactionMode, out bool _);
        }

        TableBasedQueueCache tableBasedQueueCache;
        IDelayedMessageStore delayedMessageTable;
        SqlConnectionFactory connectionFactory;
        QueueAddressTranslator addressTranslator;
        IMulticastToUnicastConverter multicastToUnicastConverter;
        static UnicastTransportOperation[] _emptyUnicastTransportOperationsList = new UnicastTransportOperation[0];
    }
}