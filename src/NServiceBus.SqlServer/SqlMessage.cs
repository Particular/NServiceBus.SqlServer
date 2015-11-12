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

        public string TransportId { get; }
        public DateTime? TimeToBeReceived { get; }
        public Stream BodyStream { get; }
        public Dictionary<string, string> Headers { get; }
    }
}