namespace CompatibilityTests.Common
{
    using System;

    public class SqlServerConnectionStringBuilder
    {
        public static string EnvironmentVariable => "SqlServer.ConnectionString";

        public static string Build()
        {
            var value = Environment.GetEnvironmentVariable(EnvironmentVariable, EnvironmentVariableTarget.User);
            return value ?? Environment.GetEnvironmentVariable(EnvironmentVariable) ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus-compat;Integrated Security=True;";
        }
    }
}
