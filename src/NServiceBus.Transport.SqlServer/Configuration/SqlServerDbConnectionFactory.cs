namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Transport.Sql.Shared;

    class SqlServerDbConnectionFactory : DbConnectionFactory
    {
        public SqlServerDbConnectionFactory(Func<CancellationToken, Task<DbConnection>> factory) : base(factory)
        {
        }

        public SqlServerDbConnectionFactory(string connectionString)
        {
            openNewConnection = async cancellationToken =>
            {
                var connection = new SqlConnection(connectionString);
                try
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                }
#pragma warning disable PS0019 // Do not catch Exception without considering OperationCanceledException
                catch (Exception)
#pragma warning restore PS0019 // Do not catch Exception without considering OperationCanceledException
                {
                    try
                    {
                        connection.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn("Failed to dispose connection.", ex);
                    }

                    throw;
                }

                return connection;
            };
        }

        static readonly ILog Logger = LogManager.GetLogger<SqlServerDbConnectionFactory>();
    }
}