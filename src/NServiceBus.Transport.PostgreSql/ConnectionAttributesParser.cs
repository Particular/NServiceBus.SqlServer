namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Data.Common;

    class ConnectionAttributesParser
    {
        public static ConnectionAttributes Parse(string connectionString, string defaultCatalog = null)
        {
            var dbConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            var connectionAttributes = new ConnectionAttributes("");

            if (defaultCatalog is not null)
            {
                connectionAttributes.Catalog = defaultCatalog;
            }
            else
            {
                if (!dbConnectionStringBuilder.TryGetValue("Database", out var catalogSetting))
                {
                    throw new Exception("Database property is mandatory in the connection string.");
                }
                connectionAttributes.Catalog = (string)catalogSetting;
            }

            return connectionAttributes;
        }
    }
}
