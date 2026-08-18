#nullable enable

namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;
using System.Transactions;
using static SqlTransportTransactionState;

/// <summary>
/// Projects the transport-specific entries stored in the <see cref="Transport.TransportTransaction"/> into a
/// typed record the dispatcher can pattern match on. The entries remain the single source of truth: the state
/// is derived on every read rather than stored alongside them, so it can never drift out of sync with the
/// contents. The storage contract is unchanged, so components that read the well-known string keys directly
/// (e.g. SQL persistence) keep working.
/// </summary>
static class TransportTransactionExtensions
{
    extension(TransportTransaction transportTransaction)
    {
        /// <summary>
        /// The context in which the transaction was created, derived from the entries present in the
        /// transaction. Transactions created by this transport record the mode through the combination of
        /// connection, native transaction and well-known flags they store. Transactions created elsewhere
        /// carry the same entries: the core creates an empty one for dispatches outside the message
        /// processing pipeline, and external integrations hand-roll instances containing a connection
        /// and/or transaction.
        /// </summary>
        public SqlTransportTransactionState State
        {
            get
            {
                transportTransaction.TryGet(TransportTransactionKeys.IsUserProvidedTransaction, out bool isUserProvided);
                transportTransaction.TryGet(TransportTransactionKeys.ReceiveOnlyTransactionMode, out bool receiveOnly);
                transportTransaction.TryGet(out Transaction? ambientTransaction);
                transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection? connection);
                transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction? nativeTransaction);

                if (isUserProvided)
                {
                    // Only a transaction may have been supplied, in which case the connection it was created on is used.
                    return new UserProvided(
                        connection ?? nativeTransaction?.Connection ?? throw new Exception($"Invalid {nameof(TransportTransaction)} state. It contains no SqlTransaction or SqlConnection objects."),
                        nativeTransaction);
                }

                if (nativeTransaction == null && ambientTransaction == null)
                {
                    return connection == null
                        ? OutsideHandler.Instance
                        : new NoTransaction(connection);
                }

                if (receiveOnly)
                {
                    return ReceiveOnly.Instance;
                }

                if (nativeTransaction != null && connection != null)
                {
                    return new SendsAtomicWithReceive(connection, nativeTransaction);
                }

                return ambientTransaction != null
                    ? AmbientTransaction.Instance
                    : throw new Exception($"{nameof(TransportTransaction)} is in invalid state.");
            }
        }
    }
}
