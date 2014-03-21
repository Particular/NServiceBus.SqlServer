namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading;
    using Settings;

    public class UnitOfWork : IDisposable
    {
        public UnitOfWork()
        {
            defaultConnectionString = SettingsHolder.Get<string>("NServiceBus.Transport.ConnectionString");
        }

        public SqlTransaction Transaction
        {
            get { return GetTransaction(defaultConnectionString); }
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
            SetTransaction(transaction, defaultConnectionString);
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
            return HasActiveTransaction(defaultConnectionString);
        }

        public bool HasActiveTransaction(string connectionString)
        {
            return currentTransactions.Value.ContainsKey(connectionString);
        }

        public void ClearTransaction()
        {
            ClearTransaction(defaultConnectionString);
        }

        public void ClearTransaction(string connectionString)
        {
            currentTransactions.Value.Remove(connectionString);
        }

        readonly ThreadLocal<Dictionary<string, SqlTransaction>> currentTransactions
            = new ThreadLocal<Dictionary<string, SqlTransaction>>(() => new Dictionary<string, SqlTransaction>(StringComparer.InvariantCultureIgnoreCase));

        string defaultConnectionString;
    }
}