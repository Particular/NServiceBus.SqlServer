namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data.Common;
    using System.Linq;

    internal class ConfigurationValidator
    {
        public bool TryValidate(List<ConnectionStringSettings> connectionSettings, out string message)
        {
            Func<string, bool> isTransportConnectionStringName = n => n.StartsWith("NServiceBus/Transport", StringComparison.InvariantCultureIgnoreCase);

            var transportConnectionSettings = connectionSettings.Where(cs => isTransportConnectionStringName(cs.Name)).ToList();

            if (transportConnectionSettings.Count() > 1)
            {
                message = @"Multidatabase setup is not supported in this version of sql transport. 
                            Please see documentation for setting up non default schema per each endpoint";

                return false;
            }

            Func<string, bool> hasSchemaOverride = cs => new DbConnectionStringBuilder { ConnectionString = cs }.ContainsKey("Queue Schema");

            if (hasSchemaOverride(transportConnectionSettings.First().ConnectionString))
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