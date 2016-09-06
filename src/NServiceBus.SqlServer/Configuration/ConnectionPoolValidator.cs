namespace NServiceBus.Transport.SQLServer
{
    using System.Data.Common;
    using System.Data.SqlClient;

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

            if (!keys.ContainsKey("Max Pool Size") || !keys.ContainsKey("Min Pool Size"))
            {
                return ValidationCheckResult.Invalid(ConnectionPoolSizeNotSet);
            }

            return ValidationCheckResult.Valid();
        }

        const string ConnectionPoolSizeNotSet = 
            "Minimum and Maximum connection pooling values are not " +
            "configured on the provided connection string.";
    }
}