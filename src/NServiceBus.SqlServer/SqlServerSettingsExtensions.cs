namespace NServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Configuration.AdvanceExtensibility;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.Config;

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
            transportExtensions.GetSettings().Set(CallbackConfig.UseCallbackReceiverSettingKey, false);
            return transportExtensions;
        }


        /// <summary>
        /// Changes the number of threads that should be used for the callback receiver. The default is 1
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="maxConcurrency">The new value for concurrency</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> CallbackReceiverMaxConcurrency(
            this TransportExtensions<SqlServerTransport> transportExtensions, 
            int maxConcurrency)
        {
            if (maxConcurrency <= 0)
            {
                throw new ArgumentException("Maximum concurrency value must be greater than zero.","maxConcurrency");
            }
            transportExtensions.GetSettings().Set(CallbackConfig.MaxConcurrencyForCallbackReceiverSettingKey, maxConcurrency);
            return transportExtensions;
        }

        /// <summary>
        /// Provides per-endpoint connection strings for multi-database support.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="connectionInformationCollection">A collection of endpoint-connection info pairs</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions, 
            IEnumerable<EndpointConnectionInfo> connectionInformationCollection)
        {
            if (connectionInformationCollection == null)
            {
                throw new ArgumentNullException("connectionInformationCollection");
            }
            if (transportExtensions.GetSettings().HasExplicitValue(ConnectionConfig.PerEndpointConnectionStringsCallbackSettingKey)
                || transportExtensions.GetSettings().HasExplicitValue(ConnectionConfig.PerEndpointConnectionStringsCollectionSettingKey))
            {
                throw new InvalidOperationException("Per-endpoint connection information can be specified only once, either by passing a collection or a callback.");
            }
            transportExtensions.GetSettings().Set(ConnectionConfig.PerEndpointConnectionStringsCollectionSettingKey, connectionInformationCollection.ToArray());
            return transportExtensions;
        }

        /// <summary>
        /// Provides per-endpoint connection strings for multi-database support.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="connectionInformationCollection">A collection of endpoint-connection info pairs</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions,
            params EndpointConnectionInfo[] connectionInformationCollection)
        {
            return UseSpecificConnectionInformation(transportExtensions, (IEnumerable<EndpointConnectionInfo>)connectionInformationCollection);
        }
        
        /// <summary>
        /// Provides per-endpoint connection strings for multi-database support.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="connectionInformationProvider">A function that gets the endpoint name and returns connection information or null if default.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> UseSpecificConnectionInformation(
            this TransportExtensions<SqlServerTransport> transportExtensions, 
            Func<string, ConnectionInfo> connectionInformationProvider)
        {
            if (connectionInformationProvider == null)
            {
                throw new ArgumentNullException("connectionInformationProvider");
            }
            if (transportExtensions.GetSettings().HasExplicitValue(ConnectionConfig.PerEndpointConnectionStringsCallbackSettingKey)
                || transportExtensions.GetSettings().HasExplicitValue(ConnectionConfig.PerEndpointConnectionStringsCollectionSettingKey))
            {
                throw new InvalidOperationException("Per-endpoint connection information can be specified only once, either by passing a collection or a callback.");
            }
            transportExtensions.GetSettings().Set(ConnectionConfig.PerEndpointConnectionStringsCallbackSettingKey, connectionInformationProvider);
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default schema (dbo) used for queue tables.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="schemaName">Name of the schema to use instead do dbo</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> DefaultSchema( this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
        {
            if (schemaName == null)
            {
                throw new ArgumentNullException("schemaName");
            }
            transportExtensions.GetSettings().Set(ConnectionConfig.DefaultSchemaSettingsKey, schemaName);
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default time to wait before triggering a circuit breaker that initiates the endpoint shutdown procedure in case there are numerous errors
        /// while trying to receive messages.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="waitTime">Time to wait before triggering the circuit breaker</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> TimeToWaitBeforeTriggeringCircuitBreaker(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan waitTime)
        {
            transportExtensions.GetSettings().Set(CircuitBreakerConfig.CircuitBreakerTimeToWaitBeforeTriggeringSettingsKey, waitTime);
            return transportExtensions;
        }

        /// <summary>
        /// Overrides the default time to pause after a failure while trying to receive a message.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="pauseTime">Time to pause after failure while receiving a message.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> PauseAfterReceiveFailure(this TransportExtensions<SqlServerTransport> transportExtensions, TimeSpan pauseTime)
        {
            transportExtensions.GetSettings().Set(CircuitBreakerConfig.CircuitBreakerDelayAfterFailureSettingsKey, pauseTime);
            return transportExtensions;
        }
    }
}