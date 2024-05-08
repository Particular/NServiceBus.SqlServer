namespace NServiceBus.AcceptanceTests
{
    using System;
    using AcceptanceTesting.Support;

    public partial class TestSuiteConstraints
    {
        public bool SupportsDtc => false;
        public bool SupportsCrossQueueTransactions => true;
        public bool SupportsNativePubSub => true;
        public bool SupportsDelayedDelivery => true;
        public bool SupportsOutbox => true;
        public bool SupportsPurgeOnStartup => true;
        public IConfigureEndpointTestExecution CreateTransportConfiguration() => new ConfigureEndpointPostgreSqlTransport();
        public IConfigureEndpointTestExecution CreatePersistenceConfiguration() => new ConfigureEndpointAcceptanceTestingPersistence();
    }
}