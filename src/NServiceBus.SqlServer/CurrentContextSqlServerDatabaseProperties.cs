namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using Pipeline;

    /// <summary>
    /// Provides users with access to the current SqlServer transport <see cref="IDbConnection"/>. 
    /// </summary>
    public class CurrentContextSqlServerDatabaseProperties
    {
        readonly PipelineExecutor pipelineExecutor;
        readonly string connectionString;

        internal CurrentContextSqlServerDatabaseProperties(PipelineExecutor pipelineExecutor, string connectionString)
        {
            this.pipelineExecutor = pipelineExecutor;
            this.connectionString = connectionString;
        }

        /// <summary>
        /// Gets the current context SqlServer transport <see cref="IDbConnection"/> or <code>null</code> if no current context SqlServer transport <see cref="IDbConnection"/> available.
        /// </summary>
        public IDbConnection Connection
        {
            get
            {
                IDbConnection connection;
                if (pipelineExecutor.CurrentContext.TryGet(string.Format("SqlConnection-{0}", connectionString), out connection))
                {
                    return connection;
                }

                return null;
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
                if (pipelineExecutor.CurrentContext.TryGet(string.Format("SqlTransaction-{0}", connectionString), out transaction))
                {
                    return transaction;
                }

                return null;
            }
        }
    }
}