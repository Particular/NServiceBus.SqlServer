namespace NServiceBus.Transport.Sql.Shared.Sending;

using System;
using System.Data.Common;
using System.Transactions;
using Receiving;

static class TransportTransactions
{
    public static TransportTransaction NoTransaction(DbConnection connection)
    {
        var transportTransaction = new TransportTransaction();
        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        return transportTransaction;
    }

    public static bool IsNoTransaction(this TransportTransaction transportTransaction, out DbConnection connection)
    {
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction nativeTransaction);
        transportTransaction.TryGet(out Transaction ambientTransaction);

        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out connection);

        return nativeTransaction == null && ambientTransaction == null;
    }

    public static TransportTransaction ReceiveOnly(DbConnection connection, DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        //this indicates to MessageDispatcher that it should not reuse connection or transaction for sends
        transportTransaction.Set(ReceiveOnlyTransactionMode, true);

        return transportTransaction;
    }

    public static bool IsReceiveOnly(this TransportTransaction transportTransaction) => transportTransaction.TryGet(ProcessWithNativeTransaction.ReceiveOnlyTransactionMode, out bool _);

    public static TransportTransaction SendsAtomicWithReceive(DbConnection connection, DbTransaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(TransportTransactionKeys.SqlConnection, connection);
        transportTransaction.Set(TransportTransactionKeys.SqlTransaction, transaction);

        return transportTransaction;
    }

    public static bool IsSendsAtomicWithReceive(this TransportTransaction transportTransaction, out DbConnection connection, out DbTransaction transaction)
    {
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out transaction);
        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out connection);
        transportTransaction.TryGet(ProcessWithNativeTransaction.ReceiveOnlyTransactionMode, out bool receiveOnly);

        return transaction != null && connection != null && !receiveOnly;
    }

    public static TransportTransaction TransactionScope(Transaction transaction)
    {
        var transportTransaction = new TransportTransaction();

        transportTransaction.Set(transaction);

        return transportTransaction;
    }

    public static bool IsTransactionScope(this TransportTransaction transportTransaction)
    {
        transportTransaction.TryGet(out Transaction ambientTransaction);
        return ambientTransaction != null;
    }

    public static bool OutsideOfHandler(this TransportTransaction transportTransaction)
    {
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction nativeTransaction);
        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbConnection nativeConnection);
        transportTransaction.TryGet(out Transaction ambientTransaction);

        return nativeTransaction == null && nativeConnection == null && ambientTransaction == null;
    }

    public static TransportTransaction UserProvided(DbConnection connection)
    {
        var result = new TransportTransaction();

        result.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);
        result.Set(TransportTransactionKeys.SqlConnection, connection);

        return result;
    }

    public static TransportTransaction UserProvided(DbTransaction transaction)
    {
        var result = new TransportTransaction();

        result.Set(TransportTransactionKeys.IsUserProvidedTransaction, true);
        result.Set(TransportTransactionKeys.SqlTransaction, transaction);

        return result;
    }

    public static bool IsUserProvided(this TransportTransaction transportTransaction, out DbConnection connection, out DbTransaction transaction)
    {
        connection = null;
        transaction = null;

        transportTransaction.TryGet(TransportTransactionKeys.IsUserProvidedTransaction, out bool isUserProvided);

        if (isUserProvided)
        {
            transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out transaction);

            if (transaction != null)
            {
                connection = transaction.Connection;
            }
            else if (transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out connection))
            {
                transaction = null;
            }
            else
            {
                throw new Exception($"Invalid {nameof(TransportTransaction)} state. Transaction provided by the user but contains no SqlTransaction or SqlConnection objects.");
            }
        }

        return isUserProvided;
    }
    internal static string ReceiveOnlyTransactionMode = "SqlTransport.ReceiveOnlyTransactionMode";
}