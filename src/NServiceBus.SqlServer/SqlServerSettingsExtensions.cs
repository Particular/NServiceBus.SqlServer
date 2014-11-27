namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using Configuration.AdvanceExtensibility;
    using NServiceBus.Transports.SQLServer;

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
            transportExtensions.GetSettings().Set(Features.SqlServerTransportFeature.UseCallbackReceiverSettingKey, false);
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
            transportExtensions.GetSettings().Set(Features.SqlServerTransportFeature.MaxConcurrencyForCallbackReceiverSettingKey, maxConcurrency);
            return transportExtensions;
        }

        /// <summary>
        /// Provides per-endpoint connection strings for multi-database support.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="connectionStrings">A collection of endpoint-connection string pairs</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseDifferentConnectionStringsForEndpoints(
            this TransportExtensions<SqlServerTransport> transportExtensions, IEnumerable<EndpointConnectionString> connectionStrings)
        {
            if (connectionStrings == null)
            {
                throw new ArgumentNullException("connectionStrings");
            }
            transportExtensions.GetSettings().Set(Features.SqlServerTransportFeature.PerEndpointConnectrionStringsSettingKey, 
                new CollectionConnectionStringProvider(connectionStrings));
            return transportExtensions;
        }
        
        /// <summary>
        /// Provides per-endpoint connection strings for multi-database support.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="connectionStringProvider">A function that gets the endpoint name and returns connection string or null if not found.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseDifferentConnectionStringsForEndpoints(
            this TransportExtensions<SqlServerTransport> transportExtensions, Func<string, string> connectionStringProvider)
        {
            if (connectionStringProvider == null)
            {
                throw new ArgumentNullException("connectionStringProvider");
            }
            transportExtensions.GetSettings().Set(Features.SqlServerTransportFeature.PerEndpointConnectrionStringsSettingKey, 
                new DelegateConnectionStringProvider(connectionStringProvider));
            return transportExtensions;
        }
    }
}