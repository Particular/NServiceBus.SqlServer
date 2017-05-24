namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;

    class Message
    {
        public Message(string transportId, Dictionary<string, string> headers, byte[] body)
        {
            TransportId = transportId;
            Body = body;
            Headers = headers;
        }

        public string TransportId { get; }
        public byte[] Body { get; }
        public Dictionary<string, string> Headers { get; }
    }
}