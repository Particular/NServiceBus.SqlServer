namespace TestSuite
{
    using System;

    static class Global
    {
        public static string ConnectionString { get; } = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? "Data source = (local); Initial catalog = nservicebus; Integrated Security = true; Encrypt=false";
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(20);
    }
}