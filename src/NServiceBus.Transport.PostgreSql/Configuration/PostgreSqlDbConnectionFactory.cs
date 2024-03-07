namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using Logging;
    using System.Threading;
    using Npgsql;
    using Sql.Shared.Configuration;

    class PostgreSqlDbConnectionFactory : DbConnectionFactory
    {
        public PostgreSqlDbConnectionFactory(Func<CancellationToken, Task<DbConnection>> factory) : base(factory)
        {
        }


        public PostgreSqlDbConnectionFactory(string connectionString)
        {
            openNewConnection = async cancellationToken =>
            {
                ValidateConnectionPool(connectionString);

                var connection = new NpgsqlConnection(connectionString);
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

        static ILog Logger = LogManager.GetLogger<PostgreSqlDbConnectionFactory>();
    }
}