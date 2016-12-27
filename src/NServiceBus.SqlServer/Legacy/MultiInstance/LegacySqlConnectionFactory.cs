namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
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
            if (HasValidated(transportAddress)) return;

            var validationResult = ConnectionPoolValidator.Validate(connectionString);
            if (!validationResult.IsValid)
            {
                Logger.Warn(validationResult.Message);
            }

            //TODO: is this correct?
            //SetValidated(transportAddress);
        }

        void SetValidated(string transportAddress)
        {
            validatedTransportConnections.Add(transportAddress);
        }

        bool HasValidated(string transportAddress)
        {
            return validatedTransportConnections.Contains(transportAddress);
        }

        Func<string, Task<SqlConnection>> openNewConnection;
        static HashSet<string> validatedTransportConnections = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        static ILog Logger = LogManager.GetLogger<LegacySqlConnectionFactory>();
    }
}