namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System;
    using Microsoft.Data.SqlClient;
    using NServiceBus.AcceptanceTests;

    public abstract class MultiCatalogAcceptanceTest : NServiceBusAcceptanceTest
    {
        protected static string GetDefaultConnectionString() =>
            Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

        protected static string WithCustomCatalog(string connectionString, string catalog)
        {
            return new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = catalog
            }.ConnectionString;
        }
    }
}
