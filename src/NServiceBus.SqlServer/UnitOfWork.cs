namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading;

    public class UnitOfWork : IDisposable
    {
        public SqlTransaction Transaction
        {
            get { return currentTransaction.Value; }
        }

        public void Dispose()
        {
            //Injected
        }

        public void SetTransaction(SqlTransaction transaction)
        {
            currentTransaction.Value = transaction;
        }

        public bool HasActiveTransaction()
        {
            return currentTransaction.IsValueCreated;
        }

        public void ClearTransaction()
        {
            currentTransaction.Value = null;
        }

        readonly ThreadLocal<SqlTransaction> currentTransaction = new ThreadLocal<SqlTransaction>();
    }
}