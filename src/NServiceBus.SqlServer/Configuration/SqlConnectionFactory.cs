namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.Common;
    using System.Threading.Tasks;
    using Logging;

    class SqlConnectionFactory
    {
        public SqlConnectionFactory(Func<Task<DbConnection>> factory)
        {
            openNewConnection = factory;
        }

        public async Task<DbConnection> OpenNewConnection()
        {
            var connection = await openNewConnection().ConfigureAwait(false);

            ValidateConnectionPool(connection.ConnectionString);

            return connection;
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

        Func<Task<DbConnection>> openNewConnection;
        static bool hasValidated;

        static ILog Logger = LogManager.GetLogger<SqlConnectionFactory>();
    }
}