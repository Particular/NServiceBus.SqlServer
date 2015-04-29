namespace NServiceBus
{
    using Configuration.AdvanceExtensibility;
    using Features;
    using Transports;

    /// <summary>
    /// SqlServer Transport
    /// </summary>
    public class SqlServerTransport : TransportDefinition
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public SqlServerTransport()
        {
            RequireOutboxConsent = true;
        }

        /// <summary>
        /// Gives implementations access to the <see cref="T:NServiceBus.BusConfiguration"/> instance at configuration time.
        /// </summary>
        protected override void Configure(BusConfiguration config)
        {
            config.EnableFeature<SqlServerTransportFeature>();
            config.EnableFeature<MessageDrivenSubscriptions>();
            config.EnableFeature<TimeoutManagerBasedDeferral>();
            config.GetSettings().EnableFeatureByDefault<StorageDrivenPublishing>();
            config.GetSettings().EnableFeatureByDefault<TimeoutManager>();
        }

        /// <summary>
        /// Creates a new Address whose Queue is derived from the Queue of the existing Address
        ///             together with the provided qualifier. For example: queue.qualifier@machine
        /// </summary>
        public override string GetSubScope(string address, string qualifier)
        {
            //TODO: Need to validate what addresses look like in sql transport
            return address + "." + qualifier;
        }
    }
}