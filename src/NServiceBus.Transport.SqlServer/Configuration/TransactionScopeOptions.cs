namespace NServiceBus
{
    using System;
    using System.Transactions;

    /// <summary>
    /// SQL Transport TransactionScope options.
    /// </summary>
    public class TransactionScopeOptions
    {
        internal TransactionScopeOptions() { }

        /// <summary>
        /// Transaction timeout.
        /// </summary>
        public TimeSpan Timeout
        {
            get;
            set
            {
                if (value > TransactionManager.MaximumTimeout)
                {
                    var message = "Timeout requested is longer than the maximum value for this machine. Override using the maxTimeout setting of the system.transactions section in machine.config";

                    throw new Exception(message);
                }
                field = value;
            }
        } = TransactionManager.DefaultTimeout;

        /// <summary>
        /// Transaction isolation level.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

        internal TransactionOptions TransactionOptions => new() { IsolationLevel = IsolationLevel, Timeout = Timeout };
    }
}