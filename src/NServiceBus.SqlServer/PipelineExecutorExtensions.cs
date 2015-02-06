namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using Janitor;
    using NServiceBus.Pipeline;

    static class PipelineExecutorExtensions
    {
        public static bool TryGetTransaction(this PipelineExecutor pipelineExecutor, string connectionString, out SqlTransaction transaction)
        {
            var key = MakeTransactionKey(connectionString);
            return pipelineExecutor.CurrentContext.TryGet(key, out transaction);
        }

        public static bool TryGetConnection(this PipelineExecutor pipelineExecutor, string connectionString, out SqlConnection connection)
        {
            var key = MakeConnectionKey(connectionString);
            return pipelineExecutor.CurrentContext.TryGet(key, out connection);
        }

        public static IDisposable SetTransaction(this PipelineExecutor pipelineExecutor, string connectionString, SqlTransaction transaction)
        {
            var key = MakeTransactionKey(connectionString);
            pipelineExecutor.CurrentContext.Set(key, transaction);
            return new ContextItemRemovalDisposable(key, pipelineExecutor);
        }

        public static IDisposable SetConnection(this PipelineExecutor pipelineExecutor, string connectionString, SqlConnection connection)
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