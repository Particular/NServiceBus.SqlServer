namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using System.Transactions;
    using JetBrains.Annotations;

    class ReceiveStrategyContext : IDisposable
    {
        public ReceiveStrategyContext([NotNull] SqlConnection connection, [NotNull] IDispatchStrategy dispatchStrategy)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (dispatchStrategy == null)
            {
                throw new ArgumentNullException(nameof(dispatchStrategy));
            }
            this.connection = connection;
            this.dispatchStrategy = dispatchStrategy;
            transportTransaction = new TransportTransaction();
            transportTransaction.Set(connection);
        }

        public ReceiveStrategyContext([NotNull] SqlConnection connection, [NotNull] SqlTransaction nativeTransaction, [NotNull] IDispatchStrategy dispatchStrategy)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (nativeTransaction == null)
            {
                throw new ArgumentNullException(nameof(nativeTransaction));
            }
            if (dispatchStrategy == null)
            {
                throw new ArgumentNullException(nameof(dispatchStrategy));
            }
            this.connection = connection;
            this.nativeTransaction = nativeTransaction;
            this.dispatchStrategy = dispatchStrategy;
            transportTransaction = new TransportTransaction();
            transportTransaction.Set(connection);
            transportTransaction.Set(nativeTransaction);
        }

        public ReceiveStrategyContext([NotNull] SqlConnection connection, [NotNull] TransactionScope ambientTransaction, [NotNull] IDispatchStrategy dispatchStrategy)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }
            if (ambientTransaction == null)
            {
                throw new ArgumentNullException(nameof(ambientTransaction));
            }
            if (dispatchStrategy == null)
            {
                throw new ArgumentNullException(nameof(dispatchStrategy));
            }
            this.connection = connection;
            this.ambientTransaction = ambientTransaction;
            this.dispatchStrategy = dispatchStrategy;
            transportTransaction = new TransportTransaction();
            transportTransaction.Set(connection);
            transportTransaction.Set(Transaction.Current);
        }

        public Task<MessageReadResult> TryReceive(TableBasedQueue queue)
        {
            return queue.TryReceive(connection, nativeTransaction);
        }

        public Task DeadLetter(TableBasedQueue queue, MessageRow poisonMessage)
        {
            return queue.DeadLetter(poisonMessage, connection, nativeTransaction);
        }

        public TransportTransaction TransportTransaction => transportTransaction;

        public IDispatchStrategy CreateDispatchStrategy()
        {
            return dispatchStrategy;
        }

        public void Rollback()
        {
            nativeTransaction?.Rollback();
        }

        public void Commit()
        {
            nativeTransaction?.Commit();
            ambientTransaction?.Complete();
        }

        public void Dispose()
        {
            //Injected
        }

        TransportTransaction transportTransaction;
        SqlConnection connection;
        SqlTransaction nativeTransaction;
        IDispatchStrategy dispatchStrategy;
        TransactionScope ambientTransaction;
    }
}