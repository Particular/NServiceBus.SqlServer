namespace NServiceBus.Transport.Sql.Shared.Queuing
{
    using System.Collections.Generic;
    using Legacy;

    public class Message
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
                ? []
                : DictionarySerializer.DeSerialize(originalHeaders);

            //TODO: figure out if we need that
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