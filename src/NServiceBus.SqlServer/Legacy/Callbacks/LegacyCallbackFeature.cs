namespace NServiceBus
{
    using NServiceBus.Features;

    class LegacyCallbackFeature : Feature
    {
        public LegacyCallbackFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register("OverrideOutgoingReplyAddressBehaviorBasedOnLegacyHeader", new OverrideOutgoingReplyAddressBehaviorBasedOnLegacyHeader(), "Overrides the destination of replies if the legacy callback header has been provided.");
        }
    }
}