namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.ObjectBuilder;
    using NServiceBus.Pipeline;

    /// <summary>
    /// Provides users with access to the current SqlServer transport <see cref="IDbConnection"/>. 
    /// </summary>
    public class SqlServerStorageContext
    {
        readonly IBuilder builder;
        readonly string connectionString;

        internal SqlServerStorageContext(IBuilder builder, string connectionString)
        {
            this.builder = builder;
            this.connectionString = connectionString;
        }

        /// <summary>
        /// Gets the current context SqlServer transport <see cref="IDbConnection"/> or <code>null</code> if no current context SqlServer transport <see cref="IDbConnection"/> available.
        /// </summary>
        public IDbConnection Connection
        {
            get
            {
                SqlConnection connection;
                return builder.Build<BehaviorContext>().TryGetConnection(connectionString, out connection) 
                    ? connection 
                    : null;
            }
        }

        /// <summary>
        /// Gets the current context SqlServer transport <see cref="SqlTransaction"/> or <code>null</code> if no current context SqlServer transport <see cref="SqlTransaction"/> available.
        /// </summary>
        public SqlTransaction Transaction
        {
            get
            {
                SqlTransaction transaction;
                return builder.Build<BehaviorContext>().TryGetTransaction(connectionString, out transaction) 
                    ? transaction 
                    : null;
            }
        }
    }
}