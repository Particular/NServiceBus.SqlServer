namespace NServiceBus.Transport.Sql.Shared;

using System;
using System.Data.Common;
using System.Transactions;

static class TransportTransactions
{
    public static TransportTransaction NoTransaction(DbConnection connection)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, new SqlTransportTransactionState.NoTransaction(connection));
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);

        return transportTransaction;
    }

    public static TransportTransaction ReceiveOnly(DbConnection connection, DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, new SqlTransportTransactionState.ReceiveOnly(connection, transaction));
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        //downstream components (e.g. SQL persistence) use this well-known entry to detect that they must not reuse the receive connection and transaction
        transportTransaction.Set(TransportTransactionKeys.ReceiveOnlyTransactionMode, true);

        return transportTransaction;
    }

    public static TransportTransaction SendsAtomicWithReceive(DbConnection connection, DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, new SqlTransportTransactionState.SendsAtomicWithReceive(connection, transaction));
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        return transportTransaction;
    }

    public static TransportTransaction TransactionScope(Transaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, new SqlTransportTransactionState.AmbientTransaction(transaction));
        transportTransaction.Set(transaction);

        return transportTransaction;
    }

    public static TransportTransaction UserProvided(DbConnection connection)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, new SqlTransportTransactionState.UserProvided(connection, null));
        transportTransaction.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);

        return transportTransaction;
    }

    public static TransportTransaction UserProvided(DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.State, new SqlTransportTransactionState.UserProvided(
            transaction.Connection ?? throw new Exception($"Invalid {nameof(TransportTransaction)} state. It contains no SqlTransaction or SqlConnection objects."),
            transaction));
        transportTransaction.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        return transportTransaction;
    }
}
