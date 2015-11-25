namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    class SqlMessage
    {
        public SqlMessage(string transportId, DateTime? timeToBeReceived, Dictionary<string, string> headers, Stream bodyStream)
        {
            TransportId = transportId;
            TimeToBeReceived = timeToBeReceived;
            BodyStream = bodyStream;
            Headers = headers;
        }

        public bool TTBRExpried(DateTime now)
        {
            return TimeToBeReceived.HasValue && TimeToBeReceived.Value < now;
        }

        public string GetLogicalId()
        {
            string logicalId;
            Headers.TryGetValue(NServiceBus.Headers.MessageId, out logicalId);

            return logicalId;
        }

        public string TransportId { get; }
        public DateTime? TimeToBeReceived { get; }
        public Stream BodyStream { get; }
        public Dictionary<string, string> Headers { get; }
    }
}