namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;

    class Message
    {
        public Message(string transportId, string originalHeaders, byte[] body, bool expired)
        {
            TransportId = transportId;
            Body = body;
            Expired = expired;
            this.originalHeaders = originalHeaders;

            InitializeHeaders();
        }

        public bool Expired { get; }
        public string TransportId { get; }
        public byte[] Body { get; }
        public Dictionary<string, string> Headers { get; private set; }

        void InitializeHeaders()
        {
            var parsedHeaders = string.IsNullOrEmpty(originalHeaders)
                ? new Dictionary<string, string>()
                : DictionarySerializer.DeSerialize(originalHeaders);

            LegacyCallbacks.SubstituteReplyToWithCallbackQueueIfExists(parsedHeaders);

            Headers = parsedHeaders;
        }

        public void ResetHeaders()
        {
            InitializeHeaders();
        }

        string originalHeaders;
    }
}