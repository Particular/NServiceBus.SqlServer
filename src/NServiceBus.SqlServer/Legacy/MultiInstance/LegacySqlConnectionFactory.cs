namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

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

            if (!HasValidatedOnce(transportAddress))
            {
                ConnectionPoolValidator.Validate(connection.ConnectionString);
                SetValidatedOnce(transportAddress);
            }

            return connection;
        }

        void SetValidatedOnce(string transportAddress)
        {
            validatedTransportConnections.Add(transportAddress);
        }

        bool HasValidatedOnce(string transportAddress)
        {
            return validatedTransportConnections.Contains(transportAddress);
        }

        Func<string, Task<SqlConnection>> openNewConnection;
        HashSet<string> validatedTransportConnections;
    }
}