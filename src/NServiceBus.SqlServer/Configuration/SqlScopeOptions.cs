namespace NServiceBus
{
    using System;
    using System.Configuration;
    using System.Transactions;
    using System.Transactions.Configuration;

    class SqlScopeOptions
    {
        public TransactionOptions TransactionOptions { get; }

        public SqlScopeOptions(TimeSpan? requestedTimeout = null, IsolationLevel? requestedIsolationLevel = null)
        {
            var timeout = TransactionManager.DefaultTimeout;

            if (requestedTimeout.HasValue)
            {
                if (requestedTimeout.Value > GetMaxTimeout())
                {
                    var message = "Timeout requested is longer than the maximum value for this machine. " +
                                  "Please override using the maxTimeout setting of the system.transactions section in machine.config";

                    throw new ConfigurationErrorsException(message);
                }

                timeout = requestedTimeout.Value;
            }

            TransactionOptions = new TransactionOptions
            {
                IsolationLevel = requestedIsolationLevel ?? IsolationLevel.ReadCommitted,
                Timeout = timeout
            };
        }

        private static TimeSpan GetMaxTimeout()
        {
            //default is always 10 minutes
            var maxTimeout = TimeSpan.FromMinutes(10);

            var systemTransactionsGroup = ConfigurationManager
                .OpenMachineConfiguration()
                .GetSectionGroup("system.transactions");

            var machineSettings = systemTransactionsGroup?.Sections.Get("machineSettings") as MachineSettingsSection;

            if (machineSettings != null)
            {
                maxTimeout = machineSettings.MaxTimeout;
            }

            return maxTimeout;
        }
    }
}