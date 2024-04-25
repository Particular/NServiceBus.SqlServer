﻿namespace NServiceBus.Transport.Sql.Shared.Sending;

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

    public static bool IsNoTransaction(this TransportTransaction transportTransaction)
    {
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction nativeTransaction);
        transportTransaction.TryGet(out Transaction ambientTransaction);

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

    public static bool IsSendsAtomicWithReceive(this TransportTransaction transportTransaction)
    {
        transportTransaction.TryGet(TransportTransactionKeys.SqlTransaction, out DbTransaction nativeTransaction);
        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbTransaction nativeConnection);
        transportTransaction.TryGet(ProcessWithNativeTransaction.ReceiveOnlyTransactionMode, out bool receiveOnly);

        return nativeTransaction != null && nativeConnection != null && !receiveOnly;
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
        transportTransaction.TryGet(TransportTransactionKeys.SqlConnection, out DbTransaction nativeConnection);
        transportTransaction.TryGet(out Transaction ambientTransaction);

        return nativeTransaction == null && nativeConnection == null && ambientTransaction == null;
    }

    internal static string ReceiveOnlyTransactionMode = "SqlTransport.ReceiveOnlyTransactionMode";
}