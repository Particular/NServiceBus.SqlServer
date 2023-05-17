namespace TestSuite
{
    using System;
    using System.Diagnostics;

    static class Global
    {
        public static string ConnectionString { get; } = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? "Data source = (local); Initial catalog = nservicebus; Integrated Security = true; Encrypt=false";
        public static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(Debugger.IsAttached ? 600 : 20);
    }
}