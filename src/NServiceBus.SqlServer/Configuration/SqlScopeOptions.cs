namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Transactions;

    class SqlScopeOptions
    {
        public SqlScopeOptions(TimeSpan? requestedTimeout = null, IsolationLevel? requestedIsolationLevel = null)
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

        public TransactionOptions TransactionOptions { get; }
    }
}