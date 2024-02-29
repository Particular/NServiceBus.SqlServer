namespace NServiceBus.Transport.SqlServer
{
    using System.Data.Common;
    using Microsoft.Data.SqlClient;

    class ConnectionPoolValidator
    {
        public static ValidationCheckResult Validate(string connectionString)
        {
            var keys = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var parsedConnection = new SqlConnectionStringBuilder(connectionString);

            if (keys.ContainsKey("Pooling") && !parsedConnection.Pooling)
            {
                return ValidationCheckResult.Valid();
            }

            if (keys.ContainsKey("Max Pool Size"))
            {
                return ValidationCheckResult.Valid();
            }
            return ValidationCheckResult.Invalid(ConnectionPoolSizeNotSet);
        }

        const string ConnectionPoolSizeNotSet =
            "Maximum connection pooling value (Max Pool Size=N) is not " +
            "configured on the provided connection string. The default value (100) will be used.";
    }
}