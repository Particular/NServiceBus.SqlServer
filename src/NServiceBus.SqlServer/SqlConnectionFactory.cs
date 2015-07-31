namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Configuration;

    /// <summary>
    /// Factory for new SQL connections. Can be configured using 
    /// <appSettings>
    ///   <add key="NServiceBus/Transport/SQLServer/connection.factory" value="NServiceBus.Transports.SQLServer.DefaultSqlConnectionFactory, NServiceBus.Transports.SQLServer"/>
    /// </appSettings>
    /// </summary>
    public static class SqlConnectionFactory
    {
        static ISqlConnectionFactory sqlConnectionFactory;
        private static object lockObj = new object();


        /// <summary>
        /// Creates instance of ISQLConnectionFactory from factory class name.
        /// </summary><param name="factoryClass">Assembly Qualified class name.</param>
        public static ISqlConnectionFactory ConnectionFactoryInstance(string factoryClass)
        {
            if (string.IsNullOrEmpty(factoryClass))
                return null;

            var factoryType = Type.GetType(factoryClass);
            if (factoryType == null)
                throw new TypeLoadException("Cannot load type " + factoryClass);

            var factoryInstance = Activator.CreateInstance(factoryType) as ISqlConnectionFactory;

            if (factoryInstance == null)
                throw new ArgumentException("Type " + factoryClass + " does not implement interface ISqlConnectionFactory.");

            return factoryInstance;
        }

        private static ISqlConnectionFactory ConfiguredConnectionFactory()
        {
            return ConnectionFactoryInstance(ConfigurationManager.AppSettings[@"NServiceBus/Transport/SQLServer/connection.factory"]);
        }

        private static void InitFactory()
        {
            if (sqlConnectionFactory != null)
                return;
            lock (lockObj)
            {
                if (sqlConnectionFactory != null)
                    return;

                var configuredFactory = ConfiguredConnectionFactory();
                sqlConnectionFactory = configuredFactory ?? new DefaultSqlConnectionFactory();
            }
        }

        /// <summary>
        /// Creates and opens new SQL connections.
        /// </summary><param name="connectionString">Database connection string.</param>
        public static SqlConnection OpenNewConnection(string connectionString)
        {
            InitFactory();

            var connection = sqlConnectionFactory.OpenNewConnection(connectionString);
            if (connection.State != ConnectionState.Open)
            {
                connection.Dispose();
                throw new Exception("Connections created by SqlConnectionFactory should be Open.");
            }

            return connection;
        }
    }
}
