namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.IO;

    class Message
    {
        public Message(string transportId, int? millisecondsToExpiry, Dictionary<string, string> headers, Stream bodyStream)
        {
            TransportId = transportId;
            TTBRExpired = millisecondsToExpiry.HasValue && millisecondsToExpiry.Value < 0;
            BodyStream = bodyStream;
            Headers = headers;
        }

        public string TransportId { get; }
        public bool TTBRExpired { get; }
        public Stream BodyStream { get; }
        public Dictionary<string, string> Headers { get; }

        public string GetLogicalId()
        {
            string logicalId;
            Headers.TryGetValue(NServiceBus.Headers.MessageId, out logicalId);

            return logicalId;
        }
    }
}