namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
#if NET452
        public bool SupportsDtc => true;
#else
        public bool SupportsDtc => false;
#endif
        public bool SupportsCrossQueueTransactions => true;
        public bool SupportsNativePubSub => false;
        public bool SupportsNativeDeferral => true;
        public bool SupportsOutbox => true;
        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointSqlServerTransport();
        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointInMemoryPersistence();
    }
}