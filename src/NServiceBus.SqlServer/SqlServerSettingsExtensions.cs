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
    }
}