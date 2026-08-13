namespace NServiceBus.Transport.Sql.Shared;

using System.Data.Common;
using System.Transactions;

static class TransportTransactions
{
    public static TransportTransaction NoTransaction(DbConnection connection) =>
        new()
        {
            State = TransportTransactionState.NoTransaction,
            Connection = connection
        };

    public static TransportTransaction ReceiveOnly(DbConnection connection, DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction
        {
            State = TransportTransactionState.ReceiveOnly,
            Connection = connection,
            NativeTransaction = transaction
        };

        //downstream components (e.g. SQL persistence) use this well-known entry to detect that they must not reuse the receive connection and transaction
        transportTransaction.Set(TransportTransactionKeys.ReceiveOnlyTransactionMode, true);

        return transportTransaction;
    }

    public static TransportTransaction SendsAtomicWithReceive(DbConnection connection, DbTransaction transaction) =>
        new()
        {
            State = TransportTransactionState.SendsAtomicWithReceive,
            Connection = connection,
            NativeTransaction = transaction
        };

    public static TransportTransaction TransactionScope(Transaction transaction)
    {
        var transportTransaction = new TransportTransaction
        {
            State = TransportTransactionState.TransactionScope
        };

        transportTransaction.Set(transaction);

        return transportTransaction;
    }

    public static TransportTransaction UserProvided(DbConnection connection)
    {
        var transportTransaction = new TransportTransaction
        {
            State = TransportTransactionState.UserProvided,
            Connection = connection
        };

        transportTransaction.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);

        return transportTransaction;
    }

    public static TransportTransaction UserProvided(DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction
        {
            State = TransportTransactionState.UserProvided,
            NativeTransaction = transaction
        };

        transportTransaction.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);

        return transportTransaction;
    }
}