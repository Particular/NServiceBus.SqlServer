namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;
    using System.IO;

    class Message
    {
        public Message(string transportId, Dictionary<string, string> headers, Stream bodyStream)
        {
            TransportId = transportId;
            BodyStream = bodyStream;
            Headers = headers;
        }

        public string TransportId { get; }
        public Stream BodyStream { get; }
        public Dictionary<string, string> Headers { get; private set; }
    }
}