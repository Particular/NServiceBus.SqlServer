namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;

    class Message
    {
        public Message(string transportId, string originalHeaders, string replyToAddress, byte[] body, bool expired)
        {
            TransportId = transportId;
            Body = body;
            Expired = expired;
            this.originalHeaders = originalHeaders;
            this.replyToAddress = replyToAddress;

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

            if (!string.IsNullOrEmpty(replyToAddress))
            {
                parsedHeaders[NServiceBus.Headers.ReplyToAddress] = replyToAddress;
            }

            LegacyCallbacks.SubstituteReplyToWithCallbackQueueIfExists(parsedHeaders);

            Headers = parsedHeaders;
        }

        public void ResetHeaders()
        {
            InitializeHeaders();
        }

        string originalHeaders;
        string replyToAddress;
    }
}