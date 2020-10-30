namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
#if NETFRAMEWORK
        public bool SupportsDtc => true;
#else
        public bool SupportsDtc => false;
#endif
        public bool SupportsCrossQueueTransactions => true;
        public bool SupportsNativePubSub => true;
        public bool SupportsDelayedDelivery => true;
        public bool SupportsOutbox => true;
        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointSqlServerTransport();
        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointAcceptanceTestingPersistence();
    }
}