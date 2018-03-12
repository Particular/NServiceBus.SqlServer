namespace NServiceBus.SqlServer.AcceptanceTests.MultiCatalog
{
    using System;
    using System.Data.SqlClient;
    using NServiceBus.AcceptanceTests;

    public class MultiCatalogAcceptanceTest : NServiceBusAcceptanceTest
    {
        protected static string GetDefaultConnectionString()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";
            }
            return connectionString;
        }

        protected static string WithCustomCatalog(string connectionString, string catalog)
        {
            return new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = catalog
            }.ConnectionString;
        }
    }
}