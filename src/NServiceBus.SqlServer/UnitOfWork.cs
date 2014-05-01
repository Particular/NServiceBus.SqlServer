namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using Pipeline;
    using Settings;

    public class UnitOfWork : IDisposable
    {
        public PipelineExecutor PipelineExecutor { get; set; }

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
            SqlTransaction transaction;
            PipelineExecutor.CurrentContext.TryGet(string.Format("SqlTransaction-{0}", connectionString), out transaction);
            return transaction;
        }

        public void SetTransaction(SqlTransaction transaction)
        {
            SetTransaction(transaction, defaultConnectionString);
        }

        public void SetTransaction(SqlTransaction transaction, string connectionString)
        {
            SqlTransaction temp;
            if(PipelineExecutor.CurrentContext.TryGet(string.Format("SqlTransaction-{0}", connectionString), out temp))
            {
                throw new InvalidOperationException("Transaction already exists for connection");
            }

            PipelineExecutor.CurrentContext.Set(string.Format("SqlTransaction-{0}", connectionString), transaction);
        }

        public bool HasActiveTransaction()
        {
            return HasActiveTransaction(defaultConnectionString);
        }

        public bool HasActiveTransaction(string connectionString)
        {
            SqlTransaction temp;
            return PipelineExecutor.CurrentContext.TryGet(string.Format("SqlTransaction-{0}", connectionString), out temp);
        }

        public void ClearTransaction()
        {
            ClearTransaction(defaultConnectionString);
        }

        public void ClearTransaction(string connectionString)
        {
            PipelineExecutor.CurrentContext.Remove(string.Format("SqlTransaction-{0}", connectionString));
        }

        string defaultConnectionString;
    }
}