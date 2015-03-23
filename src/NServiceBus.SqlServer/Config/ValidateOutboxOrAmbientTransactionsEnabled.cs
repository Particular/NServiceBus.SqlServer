namespace NServiceBus.Features
{
    using NServiceBus.Transports.SQLServer;

    class ValidateOutboxOrAmbientTransactionsEnabled : Feature
    {
        public ValidateOutboxOrAmbientTransactionsEnabled()
        {
            DependsOnAtLeastOne(typeof(Outbox), typeof(SqlServerTransportFeature));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            if (!context.Settings.GetOrDefault<bool>(typeof(Outbox).FullName)
                && context.Settings.Get<bool>("Transactions.SuppressDistributedTransactions"))
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
                    
                }
            }
        }
    }
}