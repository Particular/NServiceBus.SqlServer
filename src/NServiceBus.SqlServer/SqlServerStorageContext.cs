namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;
    using Pipeline;

    /// <summary>
    /// Provides users with access to the current SqlServer transport <see cref="IDbConnection"/>. 
    /// </summary>
    public class SqlServerStorageContext
    {
        readonly PipelineExecutor pipelineExecutor;
        readonly LocalConnectionParams localConnectionParams;

        internal SqlServerStorageContext(PipelineExecutor pipelineExecutor, LocalConnectionParams localConnectionParams)
        {
            this.pipelineExecutor = pipelineExecutor;
            this.localConnectionParams = localConnectionParams;
        }

        /// <summary>
        /// Gets the current context SqlServer transport <see cref="IDbConnection"/> or <code>null</code> if no current context SqlServer transport <see cref="IDbConnection"/> available.
        /// </summary>
        public IDbConnection Connection
        {
            get
            {
                SqlConnection connection;
                return pipelineExecutor.TryGetConnection(localConnectionParams.ConnectionString, out connection) 
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
                return pipelineExecutor.TryGetTransaction(localConnectionParams.ConnectionString, out transaction) 
                    ? transaction 
                    : null;
            }
        }
    }
}