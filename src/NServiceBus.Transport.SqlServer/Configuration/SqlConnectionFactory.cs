namespace NServiceBus.Transport.SqlServer
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using Logging;
    using System.Threading;

    class SqlConnectionFactory
    {
        public SqlConnectionFactory(Func<CancellationToken, Task<SqlConnection>> factory)
        {
            openNewConnection = factory;
        }

        public async Task<SqlConnection> OpenNewConnection(CancellationToken cancellationToken = default)
        {
            var connection = await openNewConnection(cancellationToken).ConfigureAwait(false);

            ValidateConnectionPool(connection.ConnectionString);

            return connection;
        }

        public static SqlConnectionFactory Default(string connectionString)
        {
            return new SqlConnectionFactory(async (cancellationToken) =>
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
            });
        }

        static void ValidateConnectionPool(string connectionString)
        {
            if (hasValidated)
            {
                return;
            }

            var validationResult = ConnectionPoolValidator.Validate(connectionString);
            if (!validationResult.IsValid)
            {
                Logger.Warn(validationResult.Message);
            }

            hasValidated = true;
        }

        Func<CancellationToken, Task<SqlConnection>> openNewConnection;
        static bool hasValidated;

        static ILog Logger = LogManager.GetLogger<SqlConnectionFactory>();
    }
}