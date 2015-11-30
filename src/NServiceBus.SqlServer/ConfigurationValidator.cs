namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.Common;
    using System.Linq;

    internal class ConfigurationValidator
    {
        const string TransportConnectionStringPrefix = "NServiceBus/Transport";
        const string SchemaOverridePart = "Queue Schema";

        public bool TryValidate(List<ConnectionStringSettings> connectionSettings, out string message)
        {
            Func<string, bool> isTransportConnectionStringName = n => n.StartsWith(TransportConnectionStringPrefix, StringComparison.InvariantCultureIgnoreCase);

            var transportConnectionSettings = connectionSettings.Where(cs => isTransportConnectionStringName(cs.Name)).ToList();

            //Code only configuration. Global connection string specified through code
            if (!transportConnectionSettings.Any())
            {
                message = null;
                return true;
            }

            //More than one transport connection string
            if (transportConnectionSettings.Count() > 1)
            {
                message = @"Multidatabase setup is not supported in this version of sql transport. 
                            Please see documentation for setting up non default schema per each endpoint";

                return false;
            }

            //Single connection string
            var transportConnectionString = transportConnectionSettings.Single().ConnectionString;

            Func<string, bool> isGlobalConnectionString = cs => string.Equals(cs, TransportConnectionStringPrefix, StringComparison.InvariantCultureIgnoreCase);

            if (isGlobalConnectionString(transportConnectionString) == false)
            {
               message = @"Multidatabase setup is not supported in this version of sql transport. 
                           Please see documentation for setting up non default schema per each endpoint";

                return false;
            }

            Func<string, bool> hasSchemaOverride = cs => new DbConnectionStringBuilder { ConnectionString = cs }.ContainsKey(SchemaOverridePart);

            if (hasSchemaOverride(transportConnectionString))
            {
                message = @"Schema override in connection string is not supported anymore.
                            Please see documentation for setting up non defautl scheam value";

                return false;
            }

            message = null;
            return true;
        }
    }
}