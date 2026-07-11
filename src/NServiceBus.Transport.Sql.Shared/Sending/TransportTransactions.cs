namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;
using System.Transactions;

static class TransportTransactions
{
    public static TransportTransaction NoTransaction(DbConnection connection)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, TransportTransactionState.NoTransaction);
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);

        return transportTransaction;
    }

    public static TransportTransaction ReceiveOnly(DbConnection connection, DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, TransportTransactionState.ReceiveOnly);
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        //downstream components (e.g. SQL persistence) use this well-known entry to detect that they must not reuse the receive connection and transaction
        transportTransaction.Set(TransportTransactionKeys.ReceiveOnlyTransactionMode, true);

        return transportTransaction;
    }

    public static TransportTransaction SendsAtomicWithReceive(DbConnection connection, DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, TransportTransactionState.SendsAtomicWithReceive);
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        return transportTransaction;
    }

    public static TransportTransaction TransactionScope(Transaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, TransportTransactionState.TransactionScope);
        transportTransaction.Set(transaction);

        return transportTransaction;
    }

    public static TransportTransaction UserProvided(DbConnection connection)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, TransportTransactionState.UserProvided);
        transportTransaction.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);

        return transportTransaction;
    }

    public static TransportTransaction UserProvided(DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, TransportTransactionState.UserProvided);
        transportTransaction.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        return transportTransaction;
    }

    public static TransportTransactionState GetState(this TransportTransaction transportTransaction) =>
        transportTransaction.TryGet(TransportTransactionKeys.State, out TransportTransactionState state)
            ? state
            : InferState(transportTransaction);

    /// <summary>
    /// Returns the connection to dispatch on and, if present, the transaction the sends should take part in.
    /// Falls back to the transaction's connection when only a transaction was provided.
    /// </summary>
    public static (DbConnection connection, DbTransaction transaction) GetConnectionAndTransaction(this TransportTransaction transportTransaction)
    {
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction transaction);
        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection connection);

        connection ??= transaction?.Connection
            ?? throw new Exception($"Invalid {nameof(TransportTransaction)} state. It contains no SqlTransaction or SqlConnection objects.");

        return (connection, transaction);
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

        if (ambientTransaction != null)
        {
            return TransportTransactionState.TransactionScope;
        }

        throw new Exception($"{nameof(TransportTransaction)} is in invalid state.");
    }
}
