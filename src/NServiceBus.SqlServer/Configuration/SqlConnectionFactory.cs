namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NServiceBus.Settings;

    class SqlConnectionFactory
    {
        readonly Func<string, Task<SqlConnection>> newConnectionFunc;

        public SqlConnectionFactory(Func<string, Task<SqlConnection>> connectionfactory)
        {
            newConnectionFunc = connectionfactory;
        }

        public Task<SqlConnection> OpenNewConnection(string connectionstring)
        {
            return newConnectionFunc(connectionstring);
        }

        public static SqlConnectionFactory Default(string connectionString)
        {
            return new SqlConnectionFactory(async x =>
            {

                var connection = new SqlConnection(connectionString);
                try
                {
                    await connection.OpenAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    connection.Dispose();
                    throw;
                }

                return connection;
            });

        }

        EndpointConnectionStringLookup GetEndpointConnectionStringLookup(ReadOnlySettings settings, string defaultConnectionString)
        {
            Func<string, Task<string>> endpointConnectionLookup;

            if (settings.TryGet(SettingsKeys.EndpointConnectionLookupFunc, out endpointConnectionLookup))
            {
                return new EndpointConnectionStringLookup(endpointConnectionLookup);
            }

            return EndpointConnectionStringLookup.Default(defaultConnectionString);
        }
    }

    class EndpointConnectionStringLookup
    {
        readonly Func<string, Task<string>> endpointConnectionStringLookup;

        public EndpointConnectionStringLookup(Func<string, Task<string>> lookupFunc)
        {
            endpointConnectionStringLookup = lookupFunc;
        }

        public Task<string> ConnectionStringLookup(string transportAddress)
        {
            return endpointConnectionStringLookup(transportAddress);
        }

        public static EndpointConnectionStringLookup Default(string connectionString)
        {
            return new EndpointConnectionStringLookup( transportAddress =>
            {
                return Task.FromResult(connectionString);
            });
        }

    }
}