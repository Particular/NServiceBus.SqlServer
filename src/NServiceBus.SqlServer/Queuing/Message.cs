namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;

    class Message
    {
        public Message(string transportId, Dictionary<string, string> headers, byte[] body, bool expired)
        {
            TransportId = transportId;
            Body = body;
            Expired = expired;
            Headers = headers;
        }
        public bool Expired { get; }
        public string TransportId { get; }
        public byte[] Body { get; }
        public Dictionary<string, string> Headers { get; }
    }
}