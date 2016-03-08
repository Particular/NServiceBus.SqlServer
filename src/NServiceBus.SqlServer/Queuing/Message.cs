namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    class Message
    {
        public Message(string transportId, int? millisecondsToExpiry, Dictionary<string, string> headers, Stream bodyStream)
        {
            TransportId = transportId;
            if (millisecondsToExpiry.HasValue)
            {
                TimeToBeReceived = TimeSpan.FromMilliseconds(millisecondsToExpiry.Value);
            }
            BodyStream = bodyStream;
            Headers = headers;
        }

        public bool TTBRExpired => TimeToBeReceived.HasValue && TimeToBeReceived.Value.TotalMilliseconds < 0L;

        public string GetLogicalId()
        {
            string logicalId;
            Headers.TryGetValue(NServiceBus.Headers.MessageId, out logicalId);

            return logicalId;
        }

        public string TransportId { get; }
        public TimeSpan? TimeToBeReceived { get; }
        public Stream BodyStream { get; }
        public Dictionary<string, string> Headers { get; }
    }
}