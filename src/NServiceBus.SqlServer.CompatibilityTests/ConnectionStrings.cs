namespace NServiceBus.SqlServer.CompatibilityTests
{
    using System;
    using System.Data.SqlClient;

    public static class ConnectionStrings
    {
        public static string Default = GetDefault();
        public static string Instance1 = WithCustomCatalog(Default, "nservicebus1");
        public static string Instance2 = WithCustomCatalog(Default, "nservicebus2");

        public static string Instance1_Src = WithCustomCatalog(Default, "nservicebus1") + ";Queue Schema=src";
        public static string Instance1_Dest = WithCustomCatalog(Default, "nservicebus1") + ";Queue Schema=dest";

        public static string Schema_Src = "src";
        public static string Schema_Dest = "dest";

        static string GetDefault()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";
            }
            return connectionString;
        }

        static string WithCustomCatalog(string connectionString, string catalog)
        {
            return new SqlConnectionStringBuilder(connectionString)
            {
                InitialCatalog = catalog
            }.ConnectionString;
        }
    }
}