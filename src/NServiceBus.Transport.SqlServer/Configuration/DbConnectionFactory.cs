namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.Common;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.Data.SqlClient;

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

        protected override void ValidateConnectionPool(string connectionString)
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

        static bool hasValidated;

        static ILog Logger = LogManager.GetLogger<DbConnectionFactory>();
    }
}