namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDtc => true;
        public bool SupportsCrossQueueTransactions => true;
        public bool SupportsNativePubSub => false;
        public bool SupportsNativeDeferral => true;
        public bool SupportsOutbox => true;
        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointSqlServerTransport();
        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointInMemoryPersistence();
    }
}