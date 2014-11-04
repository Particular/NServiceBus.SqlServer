namespace NServiceBus
{
    using System;
    using Configuration.AdvanceExtensibility;
    using NServiceBus.Config;

    /// <summary>
    /// Adds extra configuration for the Sql Server transport.
    /// </summary>
    public static class SqlServerSettingsExtensions
    {
        /// <summary>
        /// Disables the separate receiver that pulls messages from the machine specific callback queue.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> DisableCallbackReceiver(this TransportExtensions<SqlServerTransport> transportExtensions) 
        {
            transportExtensions.GetSettings().Set(Features.SqlServerTransport.UseCallbackReceiverSettingKey, false);
            return transportExtensions;
        }


        /// <summary>
        /// Changes the number of threads that should be used for the callback receiver. The default is 1
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="maxConcurrency">The new value for concurrency</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> CallbackReceiverMaxConcurrency(this TransportExtensions<SqlServerTransport> transportExtensions, int maxConcurrency)
        {
            if (maxConcurrency <= 0)
            {
                throw new ArgumentException("Maximum concurrency value must be greater than zero.","maxConcurrency");
            }
            transportExtensions.GetSettings().Set(Features.SqlServerTransport.MaxConcurrencyForCallbackReceiverSettingKey, maxConcurrency);
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default (dbo) schema to use when creating and accessing queue tables.
        /// If the schema does not exist it will be created when installing the host.
        /// In order to be able to send messages to endpoints that use non-default schema, the sending endpoints need to enable schema-aware addressing <see cref="EnableSchemaAwareAddressing"/>
        /// and prefix the endpoint queue table name with appropriate schema e.g. nsb.SomeEndpoint
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="schemaName">The name of the schema to use e.g. nsb</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseSchema(this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
        {
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                throw new ArgumentException("Schema name must be a non-empty string.");
            }
            transportExtensions.GetSettings().Set(Features.SqlServerTransport.SchemaName, schemaName);
            return transportExtensions;
        }

        /// <summary>
        /// Enables addressing mode that is schema-aware. When enabled, endpoint addresses in message mappings (<see cref="UnicastBusConfig"/>)
        /// need to be in form "schema.table.name" where part before the first dot denotes the schema.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="enable">Enable?</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> EnableSchemaAwareAddressing(this TransportExtensions<SqlServerTransport> transportExtensions, bool enable)
        {
            transportExtensions.GetSettings().Set(Features.SqlServerTransport.SchemaAwareAddressing, enable);
            return transportExtensions;
        }
    }
}