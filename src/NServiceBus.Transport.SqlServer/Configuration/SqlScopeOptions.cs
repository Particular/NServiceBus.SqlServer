namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Transactions;

    /// <summary>
    /// SQL Transport TransactionScope options.
    /// </summary>
    public class SqlScopeOptions
    {
        /// <summary>
        /// Configures transaction scope options.
        /// </summary>
        /// <param name="requestedTimeout">Transaction timeout.</param>
        /// <param name="requestedIsolationLevel">Transaction isolation level.</param>
        public void Configure(TimeSpan? requestedTimeout = null, IsolationLevel? requestedIsolationLevel = null)
        {
            var timeout = TransactionManager.DefaultTimeout;

            if (requestedTimeout.HasValue)
            {
                if (requestedTimeout.Value > TransactionManager.MaximumTimeout)
                {
                    var message = "Timeout requested is longer than the maximum value for this machine. Override using the maxTimeout setting of the system.transactions section in machine.config";

                    throw new Exception(message);
                }

                timeout = requestedTimeout.Value;
            }

            TransactionOptions = new TransactionOptions
            {
                IsolationLevel = requestedIsolationLevel ?? IsolationLevel.ReadCommitted,
                Timeout = timeout
            };
        }

        internal TransactionOptions TransactionOptions { get; private set; } = new TransactionOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted
        };
    }
}