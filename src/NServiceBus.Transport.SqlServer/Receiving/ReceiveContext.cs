namespace NServiceBus.Transport.SqlServer
{
    using System;
    using NServiceBus.Extensibility;

    class ReceiveContext
    {
        public DateTimeOffset StartedAt { get; private set; }
        public ContextBag Extensions { get; private set; }
        public bool WasAcknowledged { get; set; }

        public ReceiveContext()
        {
            StartedAt = DateTimeOffset.UtcNow;
            Extensions = new ContextBag();
        }

        /// <summary>
        /// Used when we discover the message has cached failure data from a previous processing attempt
        /// so that the onError uses the original data
        /// </summary>
        /// <param name="storedContext"></param>
        public void ReplaceWith(ReceiveContext storedContext)
        {
            StartedAt = storedContext.StartedAt;
            Extensions = storedContext.Extensions;
            WasAcknowledged = storedContext.WasAcknowledged;
        }
    }
}
