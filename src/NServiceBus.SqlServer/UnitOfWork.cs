namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading;

    public class UnitOfWork : IDisposable
    {
        public void Dispose()
        {
            //Injected
        }

        public SqlTransaction GetTransaction(string connectionString)
        {
            return currentTransactions.Value[connectionString];
        }

        public void SetTransaction(string connectionString, SqlTransaction transaction)
        {
            if (currentTransactions.Value.ContainsKey(connectionString))
                throw new InvalidOperationException("Transaction already exists for connection");

            currentTransactions.Value.Add(connectionString, transaction);
        }

        public bool HasActiveTransaction(string connectionString)
        {
            return currentTransactions.Value.ContainsKey(connectionString);
        }
        
        public void ClearTransaction(string connectionString)
        {
            currentTransactions.Value.Remove(connectionString);
        }

        readonly ThreadLocal<Dictionary<string, SqlTransaction>> currentTransactions
            = new ThreadLocal<Dictionary<string, SqlTransaction>>(() => new Dictionary<string, SqlTransaction>());
    }
}