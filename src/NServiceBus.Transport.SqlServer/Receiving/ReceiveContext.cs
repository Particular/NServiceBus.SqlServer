namespace NServiceBus.Transport.SqlServer
{
    using System;
    using NServiceBus.Extensibility;

    class ReceiveContext
    {
        public DateTimeOffset StartedAt { get; private set; }
        public ContextBag Extensions { get; private set; }
        public bool WasAcknowledged { get; set; }
        public bool OnMessageFailed { get; set; }

        public ReceiveContext()
        {
            StartedAt = DateTimeOffset.UtcNow;
            Extensions = new ContextBag();
        }
    }
}
