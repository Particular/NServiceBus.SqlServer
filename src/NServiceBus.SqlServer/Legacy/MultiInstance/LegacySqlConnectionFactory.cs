namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Logging;

    class LegacySqlConnectionFactory
    {
        public LegacySqlConnectionFactory(Func<string, Task<SqlConnection>> openNewConnection)
        {
            this.openNewConnection = openNewConnection;
        }

        public async Task<SqlConnection> OpenNewConnection(string queueName)
        {
            var connection = await openNewConnection(queueName).ConfigureAwait(false);

            ValidateConnectionPool(queueName, connection.ConnectionString);

            return connection;
        }

        void ValidateConnectionPool(string transportAddress, string connectionString)
        {
            if (HasValidated(transportAddress))
            {
                return;
            }
            var validationResult = ConnectionPoolValidator.Validate(connectionString);
            if (!validationResult.IsValid)
            {
                Logger.Warn(validationResult.Message);
            }
            SetValidated(transportAddress);
        }

        void SetValidated(string transportAddress)
        {
            validatedTransportConnections.AddOrUpdate(transportAddress, true, (key, value) => value);
        }

        bool HasValidated(string transportAddress)
        {
            bool _;
            return validatedTransportConnections.TryGetValue(transportAddress, out _);
        }

        Func<string, Task<SqlConnection>> openNewConnection;
        ConcurrentDictionary<string, bool> validatedTransportConnections = new ConcurrentDictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);
        static ILog Logger = LogManager.GetLogger<LegacySqlConnectionFactory>();
    }
}