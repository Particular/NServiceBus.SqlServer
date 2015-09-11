namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using Janitor;
    using NServiceBus.Pipeline;

    interface IConnectionStore
    {
        bool TryGetTransaction(string connectionString, out SqlTransaction transaction);
        bool TryGetConnection(string connectionString, out SqlConnection connection);
        IDisposable SetTransaction(string connectionString, SqlTransaction transaction);
        IDisposable SetConnection(string connectionString, SqlConnection connection);
    }

    class ContextualConnectionStore : IConnectionStore
    {
        readonly PipelineExecutor pipelineExecutor;

        public ContextualConnectionStore(PipelineExecutor pipelineExecutor)
        {
            this.pipelineExecutor = pipelineExecutor;
        }

        public bool TryGetTransaction(string connectionString, out SqlTransaction transaction)
        {
            var key = MakeTransactionKey(connectionString);
            return pipelineExecutor.CurrentContext.TryGet(key, out transaction);
        }

        public bool TryGetConnection(string connectionString, out SqlConnection connection)
        {
            var key = MakeConnectionKey(connectionString);
            return pipelineExecutor.CurrentContext.TryGet(key, out connection);
        }

        public IDisposable SetTransaction(string connectionString, SqlTransaction transaction)
        {
            var key = MakeTransactionKey(connectionString);
            pipelineExecutor.CurrentContext.Set(key, transaction);
            return new ContextItemRemovalDisposable(key, pipelineExecutor);
        }

        public IDisposable SetConnection(string connectionString, SqlConnection connection)
        {
            var key = MakeConnectionKey(connectionString);
            pipelineExecutor.CurrentContext.Set(key, connection);
            return new ContextItemRemovalDisposable(key, pipelineExecutor);
        }

        static string MakeTransactionKey(string connectionString)
        {
            return string.Format("SqlTransaction-{0}", connectionString);
        }

        static string MakeConnectionKey(string connectionString)
        {
            return string.Format("SqlConnection-{0}", connectionString);
        }

        [SkipWeaving]
        class ContextItemRemovalDisposable : IDisposable
        {
            readonly string contextKey;
            readonly PipelineExecutor pipelineExecutor;

            public ContextItemRemovalDisposable(string contextKey, PipelineExecutor pipelineExecutor)
            {
                this.contextKey = contextKey;
                this.pipelineExecutor = pipelineExecutor;
            }

            public void Dispose()
            {
                pipelineExecutor.CurrentContext.Remove(contextKey);
            }
        }
    }
}