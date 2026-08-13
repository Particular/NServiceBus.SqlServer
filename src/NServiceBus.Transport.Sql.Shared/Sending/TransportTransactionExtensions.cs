#nullable enable

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
        /// transaction was created on when only a transaction is present. Null when the transaction does not
        /// carry a connection.
        /// </summary>
        public DbConnection? Connection
        {
            get => transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection? connection)
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
        /// The native transaction the outgoing messages take part in, when there is one. Null when the
        /// transaction does not carry one.
        /// </summary>
        public DbTransaction? NativeTransaction
        {
            get => transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction? transaction) ? transaction : null;
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
        /// The context in which the transaction was created, derived from the entries present in the
        /// transaction. The transport records the mode implicitly at creation time through the combination
        /// of connection, native transaction and well-known flags it stores, so the state can never drift
        /// out of sync with the contents.
        /// </summary>
        public TransportTransactionState State
        {
            get
            {
                transportTransaction.TryGet(TransportTransactionKeys.IsUserProvidedTransaction, out bool isUserProvided);
                transportTransaction.TryGet(TransportTransactionKeys.ReceiveOnlyTransactionMode, out bool receiveOnly);
                transportTransaction.TryGet(out Transaction? ambientTransaction);

                // The Connection property falls back to the transaction's connection, so the raw entry is
                // checked here to detect a native transaction without a stored connection, which is invalid.
                transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection? connection);

                if (isUserProvided)
                {
                    return TransportTransactionState.UserProvided;
                }

                if (transportTransaction.NativeTransaction == null && ambientTransaction == null)
                {
                    return connection == null
                        ? TransportTransactionState.OutsideHandler
                        : TransportTransactionState.NoTransaction;
                }

                if (receiveOnly)
                {
                    return TransportTransactionState.ReceiveOnly;
                }

                if (transportTransaction.NativeTransaction != null && connection != null)
                {
                    return TransportTransactionState.SendsAtomicWithReceive;
                }

                return ambientTransaction != null
                    ? TransportTransactionState.TransactionScope
                    : throw new Exception($"{nameof(TransportTransaction)} is in invalid state.");
            }
        }
    }
}