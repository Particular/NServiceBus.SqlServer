namespace NServiceBus.Transports.SQLServer.Legacy.MultiInstance
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;

    class LegacySqlConnectionFactory
    {
        public LegacySqlConnectionFactory(Func<string, Task<SqlConnection>> factory)
        {
            this.factory = factory;
        }

        public Task<SqlConnection> OpenNewConnection(string transportAddress)
        {
            return factory(transportAddress);
        }

        Func<string, Task<SqlConnection>>  factory;
    }
}