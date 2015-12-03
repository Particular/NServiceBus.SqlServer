namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Features;

    class SqlServerConfigurator : Feature
    {
        SqlServerConfigurator()
        {
            //TODO: this is something that needs discussing. What happens when both msmq and sql are enabled (which will be the case probably)
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
        }
    }
}