namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Data.SqlClient;
    using Sql.Shared.Configuration;

    class SqlServerDbConnectionFactory : DbConnectionFactory
    {
        public SqlServerDbConnectionFactory(Func<CancellationToken, Task<DbConnection>> factory) : base(factory)
        {
        }


        public SqlServerDbConnectionFactory(string connectionString)
        {
            openNewConnection = async cancellationToken =>
            {
                ValidateConnectionPool(connectionString);

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

        static ILog Logger = LogManager.GetLogger<SqlServerDbConnectionFactory>();
    }
}