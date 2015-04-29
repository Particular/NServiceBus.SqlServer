namespace NServiceBus.Transports.SQLServer.Config
{
    using NServiceBus.Features;
    using NServiceBus.Settings;
    using NServiceBus.Support;

    class CallbackConfig : ConfigBase
    {
        public const string CallbackHeaderKey = "NServiceBus.SqlServer.CallbackQueue";

        public const string UseCallbackReceiverSettingKey = "SqlServer.UseCallbackReceiver";

        public override void SetUpDefaults(SettingsHolder settings)
        {
            settings.SetDefault(UseCallbackReceiverSettingKey, true);
        }

        public override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            context.Pipeline.Register<ReadIncomingCallbackAddressBehavior.Registration>();

            var useCallbackReceiver = context.Settings.Get<bool>(UseCallbackReceiverSettingKey);
            var queueName = context.Settings.EndpointName();
            var callbackQueue = string.Format("{0}.{1}", queueName, RuntimeEnvironment.MachineName);
            if (useCallbackReceiver)
            {
                context.Container.ConfigureComponent<CallbackQueueCreator>(DependencyLifecycle.InstancePerCall)
                    .ConfigureProperty(p => p.Enabled, true)
                    .ConfigureProperty(p => p.CallbackQueueAddress, callbackQueue);

                context.Pipeline.Register<SetOutgoingCallbackAddressBehavior.Registration>();
                context.Container.ConfigureComponent(c => new OutgoingCallbackAddressSetter(callbackQueue), DependencyLifecycle.SingleInstance);
            }
            context.Container.RegisterSingleton(new SecondaryReceiveConfiguration(workQueue =>
            {
                //if this isn't the main queue we shouldn't use callback receiver
                if (!useCallbackReceiver || workQueue != queueName)
                {
                    return SecondaryReceiveSettings.Disabled();
                }

                return SecondaryReceiveSettings.Enabled(callbackQueue);
            }));
        }
    }
}