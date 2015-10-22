namespace NServiceBus.Transports.SQLServer.Light
{
    using NServiceBus.Features;

    /// <summary>
    /// 
    /// </summary>
    class SqlServerConfigurator : ConfigureTransport
    {
        internal SqlServerConfigurator()
        {
            //TODO: figure out what do we want to depend on
            //DependsOn<UnicastBus>();
        }

        protected override void Configure(FeatureConfigurationContext context, string connectionString)
        {
            context.Container.ConfigureComponent(b => new SqlServerMessageSender(new TableBasedQueue("", "", connectionString)), DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent(b => new SqlServerQueueCreator(connectionString), DependencyLifecycle.InstancePerCall);

            context.Container.ConfigureComponent(b => new MessagePump(connectionString), DependencyLifecycle.InstancePerCall);
        }

        protected override string ExampleConnectionStringForErrorMessage  =>  @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True"; 
    }
}