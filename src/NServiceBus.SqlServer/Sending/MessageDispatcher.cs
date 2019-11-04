﻿namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Transactions;
    using DelayedDelivery;
    using DeliveryConstraints;
    using Extensibility;
    using Performance.TimeToBeReceived;
    using Transport;

    class MessageDispatcher : IDispatchMessages
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
        public async Task Dispatch(TransportOperations operations, TransportTransaction transportTransaction, ContextBag context)
        {
            var sortedOperations = operations.UnicastTransportOperations
                .Concat(await ConvertToUnicastOperations(operations).ConfigureAwait(false))
                .SortAndDeduplicate(addressTranslator);

            await DispatchDefault(sortedOperations, transportTransaction).ConfigureAwait(false);
            await DispatchIsolated(sortedOperations).ConfigureAwait(false);
        }

        async Task<List<UnicastTransportOperation>> ConvertToUnicastOperations(TransportOperations operations)
        {
            var tasks = operations.MulticastTransportOperations.Select(multicastToUnicastConverter.Convert).ToArray();
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.SelectMany(t => t.Result).ToList();
        }

        async Task DispatchIsolated(SortingResult sortedOperations)
        {
            if (sortedOperations.IsolatedDispatch == null)
            {
                return;
            }

#if NET452
            using (var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                await Dispatch(sortedOperations.IsolatedDispatch, connection, null).ConfigureAwait(false);

                scope.Complete();
            }
#else
            using (var scope = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled))
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            using (var tx = connection.BeginTransaction())
            {
                await Dispatch(sortedOperations.IsolatedDispatch, connection, tx).ConfigureAwait(false);
                tx.Commit();
                scope.Complete();
            }
#endif

        }

        async Task DispatchDefault(SortingResult sortedOperations, TransportTransaction transportTransaction)
        {
            if (sortedOperations.DefaultDispatch == null)
            {
                return;
            }

            if (InReceiveWithNoTransactionMode(transportTransaction) || InReceiveOnlyTransportTransactionMode(transportTransaction))
            {
                await DispatchUsingNewConnectionAndTransaction(sortedOperations.DefaultDispatch).ConfigureAwait(false);
                return;
            }

            await DispatchUsingReceiveTransaction(transportTransaction, sortedOperations.DefaultDispatch).ConfigureAwait(false);
        }


        async Task DispatchUsingNewConnectionAndTransaction(IEnumerable<UnicastTransportOperation> operations)
        {
            using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var transaction = connection.BeginTransaction())
                {
                    await Dispatch(operations, connection, transaction).ConfigureAwait(false);
                    transaction.Commit();
                }
            }
        }

        async Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, IEnumerable<UnicastTransportOperation> operations)
        {
            transportTransaction.TryGet(out SqlConnection sqlTransportConnection);
            transportTransaction.TryGet(out SqlTransaction sqlTransportTransaction);
            transportTransaction.TryGet(out Transaction ambientTransaction);

            if (ambientTransaction != null)
            {
                using (var connection = await connectionFactory.OpenNewConnection().ConfigureAwait(false))
                {
                    await Dispatch(operations, connection, null).ConfigureAwait(false);
                }
            }
            else
            {
                await Dispatch(operations, sqlTransportConnection, sqlTransportTransaction).ConfigureAwait(false);
            }
        }

        async Task Dispatch(IEnumerable<UnicastTransportOperation> operations, SqlConnection connection, SqlTransaction transaction)
        {
            foreach (var operation in operations)
            {
                await Dispatch(connection, transaction, operation).ConfigureAwait(false);
            }
        }

        Task Dispatch(SqlConnection connection, SqlTransaction transaction, UnicastTransportOperation operation)
        {
            TryGetConstraint(operation, out DiscardIfNotReceivedBefore discardIfNotReceivedBefore);
            if (TryGetConstraint(operation, out DoNotDeliverBefore doNotDeliverBefore))
            {
                if (discardIfNotReceivedBefore != null && discardIfNotReceivedBefore.MaxTime < TimeSpan.MaxValue)
                {
                    throw new Exception("Delayed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to delay messages of this type.");
                }

                return delayedMessageTable.Store(operation.Message, doNotDeliverBefore.At - DateTime.UtcNow, operation.Destination, connection, transaction);
            }
            if (TryGetConstraint(operation, out DelayDeliveryWith delayDeliveryWith))
            {
                if (discardIfNotReceivedBefore != null && discardIfNotReceivedBefore.MaxTime < TimeSpan.MaxValue)
                {
                    throw new Exception("Delayed delivery of messages with TimeToBeReceived set is not supported. Remove the TimeToBeReceived attribute to delay messages of this type.");
                }

                return delayedMessageTable.Store(operation.Message, delayDeliveryWith.Delay, operation.Destination, connection, transaction);
            }

            var queue = tableBasedQueueCache.Get(operation.Destination);
            return queue.Send(operation.Message, discardIfNotReceivedBefore?.MaxTime ?? TimeSpan.MaxValue, connection, transaction);
        }

        static bool InReceiveWithNoTransactionMode(TransportTransaction transportTransaction)
        {
            transportTransaction.TryGet(out SqlTransaction nativeTransaction);
            transportTransaction.TryGet(out Transaction ambientTransaction);

            return nativeTransaction == null && ambientTransaction == null;
        }

        static bool InReceiveOnlyTransportTransactionMode(TransportTransaction transportTransaction)
        {
            return transportTransaction.TryGet(ProcessWithNativeTransaction.ReceiveOnlyTransactionMode, out bool _);
        }

        static bool TryGetConstraint<T>(IOutgoingTransportOperation operation, out T constraint) where T : DeliveryConstraint
        {
            constraint = operation.DeliveryConstraints.OfType<T>().FirstOrDefault();
            return constraint != null;
        }

        TableBasedQueueCache tableBasedQueueCache;
        IDelayedMessageStore delayedMessageTable;
        SqlConnectionFactory connectionFactory;
        QueueAddressTranslator addressTranslator;
        IMulticastToUnicastConverter multicastToUnicastConverter;


    }
}