namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using Janitor;
    using NServiceBus.Pipeline;

    static class PipelineExecutorExtensions
    {
        public static bool TryGetTransaction(this BehaviorContext context, string connectionString, out SqlTransaction transaction)
        {
            var key = MakeTransactionKey(connectionString);
            return context.TryGet(key, out transaction);
        }

        public static bool TryGetConnection(this BehaviorContext context, string connectionString, out SqlConnection connection)
        {
            var key = MakeConnectionKey(connectionString);
            return context.TryGet(key, out connection);
        }

        public static IDisposable SetTransaction(this BehaviorContext context, string connectionString, SqlTransaction transaction)
        {
            var key = MakeTransactionKey(connectionString);
            context.Set(key, transaction);
            return new ContextItemRemovalDisposable(key, context);
        }

        public static IDisposable SetConnection(this BehaviorContext context, string connectionString, SqlConnection connection)
        {
            var key = MakeConnectionKey(connectionString);
            context.Set(key, connection);
            return new ContextItemRemovalDisposable(key, context);
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
            readonly BehaviorContext context;

            public ContextItemRemovalDisposable(string contextKey, BehaviorContext context)
            {
                this.contextKey = contextKey;
                this.context = context;
            }

            public void Dispose()
            {
                context.Remove(contextKey);
            }
        }
    }
}