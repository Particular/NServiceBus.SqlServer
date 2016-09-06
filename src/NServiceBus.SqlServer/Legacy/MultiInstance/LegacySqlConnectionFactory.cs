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
            validatedTransportConnections = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public async Task<SqlConnection> OpenNewConnection(string transportAddress)
        {
            var connection = await openNewConnection(transportAddress).ConfigureAwait(false);

            ValidateConnectionPool(transportAddress, connection.ConnectionString);

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

            SetValidated(transportAddress);
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
        HashSet<string> validatedTransportConnections;
        static ILog Logger = LogManager.GetLogger<LegacySqlConnectionFactory>();
    }
}