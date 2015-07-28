namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading;

    public class UnitOfWork : IDisposable
    {
        public SqlTransaction Transaction
        {
            get { return GetTransaction(DefaultConnectionString); }
        }

        public void Dispose()
        {
            //Injected
        }

        public SqlTransaction GetTransaction(string connectionString)
        {
            return currentTransactions.Value[connectionString];
        }

        public void SetTransaction(SqlTransaction transaction)
        {
            SetTransaction(transaction, DefaultConnectionString);
        }

        public void SetTransaction(SqlTransaction transaction, string connectionString)
        {
            if (currentTransactions.Value.ContainsKey(connectionString))
            {
                throw new InvalidOperationException("Transaction already exists for connection");
            }

            currentTransactions.Value.Add(connectionString, transaction);
        }

        public bool HasActiveTransaction()
        {
            return HasActiveTransaction(DefaultConnectionString);
        }

        public bool HasActiveTransaction(string connectionString)
        {
            return currentTransactions.Value.ContainsKey(connectionString);
        }

        public void ClearTransaction()
        {
            ClearTransaction(DefaultConnectionString);
        }

        public void ClearTransaction(string connectionString)
        {
            currentTransactions.Value.Remove(connectionString);
        }

        ThreadLocal<Dictionary<string, SqlTransaction>> currentTransactions
            = new ThreadLocal<Dictionary<string, SqlTransaction>>(() => new Dictionary<string, SqlTransaction>(StringComparer.InvariantCultureIgnoreCase));

        public string DefaultConnectionString { get; set; }
    }
}