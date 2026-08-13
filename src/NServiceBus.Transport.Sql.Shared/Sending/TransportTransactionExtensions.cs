namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;
using System.Transactions;

/// <summary>
/// Lifts the transport-specific entries stored in the <see cref="Transport.TransportTransaction"/> into
/// typed properties. The storage contract is unchanged: the well-known string keys keep being written so
/// components that read them directly (e.g. SQL persistence) keep working.
/// </summary>
static class TransportTransactionExtensions
{
    extension(TransportTransaction transportTransaction)
    {
        /// <summary>
        /// The connection the outgoing messages are dispatched on. Falls back to the connection the native
        /// transaction was created on when only a transaction is present.
        /// </summary>
        public DbConnection Connection
        {
            get => transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection connection)
                ? connection
                : transportTransaction.NativeTransaction?.Connection;
            set
            {
                if (value == null)
                {
                    transportTransaction.Remove(TransportTransactionKeys.SqlConnection);
                }
                else
                {
                    transportTransaction.Set(TransportTransactionKeys.SqlConnection, value);
                }
            }
        }

        /// <summary>
        /// The native transaction the outgoing messages take part in, when there is one.
        /// </summary>
        public DbTransaction NativeTransaction
        {
            get => transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction transaction) ? transaction : null;
            set
            {
                if (value == null)
                {
                    transportTransaction.Remove(TransportTransactionKeys.SqlTransaction);
                }
                else
                {
                    transportTransaction.Set(TransportTransactionKeys.SqlTransaction, value);
                }
            }
        }

        /// <summary>
        /// The context in which the transaction was created. Recorded explicitly by this transport at creation
        /// time; inferred from the entries present for transactions that were created outside of it.
        /// </summary>
        public TransportTransactionState State
        {
            get => transportTransaction.TryGet(TransportTransactionKeys.State, out TransportTransactionState state)
                ? state
                : InferState(transportTransaction);
            set => transportTransaction.Set(TransportTransactionKeys.State, value);
        }
    }

    // TransportTransaction instances that were not created by this transport carry no explicit state: the core
    // creates an empty one for dispatches outside the message processing pipeline, and external integrations
    // hand-roll instances containing a connection and/or transaction. For those the state is derived from the
    // entries present in the transaction.
    static TransportTransactionState InferState(TransportTransaction transportTransaction)
    {
        transportTransaction.TryGet(TransportTransactionKeys.IsUserProvidedTransaction, out bool isUserProvided);
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction nativeTransaction);
        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection connection);
        transportTransaction.TryGet(out Transaction ambientTransaction);

        if (isUserProvided)
        {
            return TransportTransactionState.UserProvided;
        }

        if (nativeTransaction == null && ambientTransaction == null)
        {
            return connection == null
                ? TransportTransactionState.OutsideHandler
                : TransportTransactionState.NoTransaction;
        }

        if (transportTransaction.TryGet(TransportTransactionKeys.ReceiveOnlyTransactionMode, out bool _))
        {
            return TransportTransactionState.ReceiveOnly;
        }

        if (nativeTransaction != null && connection != null)
        {
            return TransportTransactionState.SendsAtomicWithReceive;
        }

        return ambientTransaction != null ? TransportTransactionState.TransactionScope : throw new Exception($"{nameof(TransportTransaction)} is in invalid state.");
    }
}