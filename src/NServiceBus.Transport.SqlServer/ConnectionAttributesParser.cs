namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Data.Common;
    using Sql;

    class ConnectionAttributesParser
    {
        public static ConnectionAttributes Parse(string connectionString, string defaultCatalog = null)
        {
            var dbConnectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            var connectionAttributes = new ConnectionAttributes("", false);

            if (defaultCatalog is not null)
            {
                connectionAttributes.Catalog = defaultCatalog;
            }
            else
            {
                if (!dbConnectionStringBuilder.TryGetValue("Initial Catalog", out var catalogSetting) && !dbConnectionStringBuilder.TryGetValue("database", out catalogSetting))
                {
                    throw new Exception("Initial Catalog property is mandatory in the connection string.");
                }
                connectionAttributes.Catalog = (string)catalogSetting;
            }

            if (dbConnectionStringBuilder.TryGetValue("Column Encryption Setting", out var enabled))
            {
                connectionAttributes.IsEncrypted = ((string)enabled).Equals("enabled", StringComparison.InvariantCultureIgnoreCase);
            }

            return connectionAttributes;
        }
    }
}
