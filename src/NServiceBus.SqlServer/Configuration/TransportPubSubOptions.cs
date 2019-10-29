namespace NServiceBus.Transport.SQLServer
{
    using System;

    class TransportPubSubOptions
    {
        public TimeSpan? TimeToCacheSubscription { get; set; }
    }
}