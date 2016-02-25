namespace NServiceBus.Transports.SQLServer
{
    using System;

    class PurgeExpiredMessagesParams
    {
        public TimeSpan PurgeTaskDelay { get; set; }
        public int PurgeBatchSize { get; set; }
    }
}