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
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TransactionManager.MaximumTimeout);
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