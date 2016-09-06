namespace NServiceBus.Transport.SQLServer
{
    using System.Data.Common;
    using System.Data.SqlClient;
    using Logging;

    class ConnectionPoolValidator
    {
        public static bool Validate(string connectionString)
        {
            var keys = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var parsedConnection = new SqlConnectionStringBuilder(connectionString);

            if (keys.ContainsKey("Pooling") && !parsedConnection.Pooling)
            {
                Logger.Warn(ConnectionPoolingDisabled);
                return false;
            }

            if (!keys.ContainsKey("Max Pool Size") || !keys.ContainsKey("Min Pool Size"))
            {
                Logger.Warn(ConnectionPoolSizeNotSet);
                return false;
            }

            return true;
        }

        static ILog Logger = LogManager.GetLogger(typeof(ConnectionPoolValidator));

        const string ConnectionPoolingDisabled = 
            "Disabling connection pooling is not recommended. Consider " +
            "enabling it and specifying Mininum and Maximum pool size.";

        const string ConnectionPoolSizeNotSet = 
            "Minimum and Maximum connection pooling values are not " +
            "configured on the provided connection string.";
    }
}