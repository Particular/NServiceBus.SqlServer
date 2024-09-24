namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Transport.Sql.Shared;
    using Sql.Shared.Configuration;

    class SqlServerDbConnectionFactory : DbConnectionFactory
    {
        public SqlServerDbConnectionFactory(Func<CancellationToken, Task<DbConnection>> factory, Func<string, ValidationCheckResult> connectionPoolValidator) : base(factory, connectionPoolValidator)
        {
        }


        public SqlServerDbConnectionFactory(string connectionString, Func<string, ValidationCheckResult> connectionPoolValidator)
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

            validateConnectionPool = connectionPoolValidator;
        }

        static ILog Logger = LogManager.GetLogger<SqlServerDbConnectionFactory>();
    }
}