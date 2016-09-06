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
                return ValidationCheckResult.Invalid(ConnectionPoolingDisabled);
            }

            if (!keys.ContainsKey("Max Pool Size") || !keys.ContainsKey("Min Pool Size"))
            {
                return ValidationCheckResult.Invalid(ConnectionPoolSizeNotSet);
            }

            return ValidationCheckResult.Valid();
        }

        const string ConnectionPoolingDisabled = 
            "Disabling connection pooling is not recommended. Consider " +
            "enabling it and specifying Mininum and Maximum pool size.";

        const string ConnectionPoolSizeNotSet = 
            "Minimum and Maximum connection pooling values are not " +
            "configured on the provided connection string.";
    }
}