#nullable enable

namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;
using System.Transactions;

/// <summary>
/// Lifts the transport-specific state stored in the <see cref="Transport.TransportTransaction"/> into a
/// typed record the dispatcher can pattern match on. The storage contract is unchanged: the well-known
/// string keys keep being written so components that read them directly (e.g. SQL persistence) keep working.
/// </summary>
static class TransportTransactionExtensions
{
    extension(TransportTransaction transportTransaction)
    {
        /// <summary>
        /// The context in which the transaction was created. Transport-created transactions carry the state
        /// explicitly; for transactions created outside the transport the state is derived from the entries
        /// present in the transaction.
        /// </summary>
        public SqlTransportTransactionState State =>
            transportTransaction.TryGet(TransportTransactionKeys.State, out SqlTransportTransactionState? state)
                ? state!
                : InferState(transportTransaction);
    }

    // TransportTransaction instances that were not created by this transport carry no explicit state: the core
    // creates an empty one for dispatches outside the message processing pipeline, and external integrations
    // hand-roll instances containing a connection and/or transaction. For those the state is derived from the
    // entries present in the transaction.
    static SqlTransportTransactionState InferState(TransportTransaction transportTransaction)
    {
        transportTransaction.TryGet(TransportTransactionKeys.IsUserProvidedTransaction, out bool isUserProvided);
        transportTransaction.TryGet(TransportTransactionKeys.ReceiveOnlyTransactionMode, out bool receiveOnly);
        transportTransaction.TryGet(out Transaction? ambientTransaction);
        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection? connection);
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction? nativeTransaction);

        if (isUserProvided)
        {
            return new SqlTransportTransactionState.UserProvided(
                connection ?? nativeTransaction?.Connection ?? throw new Exception($"Invalid {nameof(TransportTransaction)} state. It contains no SqlTransaction or SqlConnection objects."),
                nativeTransaction);
        }

        if (nativeTransaction == null && ambientTransaction == null)
        {
            return connection == null
                ? SqlTransportTransactionState.OutsideHandler.Instance
                : new SqlTransportTransactionState.NoTransaction(connection);
        }

        if (receiveOnly)
        {
            return new SqlTransportTransactionState.ReceiveOnly(connection!, nativeTransaction!);
        }

        if (nativeTransaction != null && connection != null)
        {
            return new SqlTransportTransactionState.SendsAtomicWithReceive(connection, nativeTransaction);
        }

        return ambientTransaction != null
            ? new SqlTransportTransactionState.AmbientTransaction(ambientTransaction)
            : throw new Exception($"{nameof(TransportTransaction)} is in invalid state.");
    }
}
