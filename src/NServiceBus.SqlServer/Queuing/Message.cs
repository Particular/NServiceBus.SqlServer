namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;

    class Message
    {
        public Message(string transportId, Dictionary<string, string> headers, byte[] body, string destination)
        {
            TransportId = transportId;
            Body = body;
            Destination = destination;
            Headers = headers;
        }

        public string TransportId { get; }
        public byte[] Body { get; }
        public string Destination { get; }
        public Dictionary<string, string> Headers { get; }
    }
}