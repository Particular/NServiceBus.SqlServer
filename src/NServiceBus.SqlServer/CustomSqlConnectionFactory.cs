namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class CustomSqlConnectionFactory
    {
        public readonly Func<string, SqlConnection> OpenNewConnection;

        public CustomSqlConnectionFactory(Func<string, SqlConnection> factory)
        {
            this.OpenNewConnection = factory;
        }
    }
}
