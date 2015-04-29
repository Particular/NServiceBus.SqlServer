namespace NServiceBus.Features
{
    using System;
    using NServiceBus.Transports.SQLServer;

    class ValidateOutboxOrAmbientTransactionsEnabled : Feature
    {
        public ValidateOutboxOrAmbientTransactionsEnabled()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            if (!context.Settings.GetOrDefault<bool>(typeof(Outbox).FullName)
                && context.Settings.Get<bool>("Transactions.SuppressDistributedTransactions")
                && context.Settings.Get<bool>("Transactions.Enabled"))
            {
                RegisterStartupTask<ValiateTask>();
            }
        }

        class ValiateTask : FeatureStartupTask
        {
            readonly IConnectionStringProvider connectionStringProvider;

            public ValiateTask(IConnectionStringProvider connectionStringProvider)
            {
                this.connectionStringProvider = connectionStringProvider;
            }

            protected override void OnStart()
            {
                if (connectionStringProvider.AllowsNonLocalConnectionString)
                {
                    throw new Exception(@"The transport is running in native SQL Server transactions mode without an outbox, but configuration uses "
                        + "multi-database sending (http://docs.particular.net/nservicebus/sqlserver/multiple-databases). Multi-database sends can only be used when either in ambient transaction mode "
                        + "or when outbox is enabled. Please check your endpoint configuration.");
                }
            }
        }
    }
}