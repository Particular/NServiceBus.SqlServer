namespace NServiceBus.Transport.SQLServer
{
    using Features;

    class PreventRoutingMessagesToTimeoutManager : Feature
    {
        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Remove("RouteDeferredMessageToTimeoutManager");
        }
    }
}