
namespace NServiceBus.Transports.SQLServer.Config
{
    using System;
    using NServiceBus.CircuitBreakers;
    using NServiceBus.Features;
    using NServiceBus.Settings;

    class CircuitBreakerConfig : ConfigBase
    {
        internal delegate RepeatedFailuresOverTimeCircuitBreaker CircuitBreakerFactory(CriticalError criticalError, TimeSpan timeToWaitBeforeTriggering, TimeSpan delayAfterFailure);

        public const string CircuitBreakerTimeToWaitBeforeTriggeringSettingsKey = "SqlServer.CircuitBreaker.TimeToWaitBeforeTriggering";
        public const string CircuitBreakerDelayAfterFailureSettingsKey = "SqlServer.CircuitBreaker.DelayAfterFailure";

        public override void Configure(FeatureConfigurationContext context, string connectionStringWithSchema)
        {
            context.Container.ConfigureComponent(b => circuitBreakerFactory(b.Build<CriticalError>(),
                context.Settings.Get<TimeSpan>(CircuitBreakerTimeToWaitBeforeTriggeringSettingsKey),
                context.Settings.Get<TimeSpan>(CircuitBreakerDelayAfterFailureSettingsKey)), DependencyLifecycle.InstancePerCall);
        }

        public override void SetUpDefaults(SettingsHolder settings)
        {
            settings.SetDefault(CircuitBreakerTimeToWaitBeforeTriggeringSettingsKey, TimeSpan.FromMinutes(2));
            settings.SetDefault(CircuitBreakerDelayAfterFailureSettingsKey, TimeSpan.FromSeconds(10));
        }

        public CircuitBreakerConfig()
            : this(SetupCircuitBreaker)
        {
        }

        public CircuitBreakerConfig(CircuitBreakerFactory circuitBreakerFactory)
        {
            this.circuitBreakerFactory = circuitBreakerFactory;
        }

        CircuitBreakerFactory circuitBreakerFactory;

        static RepeatedFailuresOverTimeCircuitBreaker SetupCircuitBreaker(CriticalError criticalError, TimeSpan timeToWaitBeforeTriggering, TimeSpan delayAfterFailure)
        {
            return new RepeatedFailuresOverTimeCircuitBreaker("SqlTransportConnectivity",
                timeToWaitBeforeTriggering,
                ex => criticalError.Raise("Repeated failures when communicating with SQL",
                    ex), delayAfterFailure);
        }
    }
}
