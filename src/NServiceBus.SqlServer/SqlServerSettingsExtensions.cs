namespace NServiceBus
{
    using System;
    using Configuration.AdvanceExtensibility;

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
        /// Overrides default dead letter queue name.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="queueName">Name of dead letter queue.</param>
        /// <returns></returns>
        public static TransportExtensions<SqlServerTransport> DeadLetterQueue(this TransportExtensions<SqlServerTransport> transportExtensions, string queueName)
        {
            if (string.IsNullOrWhiteSpace(queueName))
            {
                throw new ArgumentException("Dead letter queue name can not be empty string.","queueName");
            }
            transportExtensions.GetSettings().Set(Features.SqlServerTransportFeature.DeadLetterQueueName, queueName);
            return transportExtensions;
        }
    }
}