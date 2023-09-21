namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using NServiceBus.AcceptanceTests;

    public class MultiCatalogAcceptanceTest : NServiceBusAcceptanceTest
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