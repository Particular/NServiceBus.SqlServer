namespace NServiceBus.Transports.SQLServer
{
    class Subscriber
    {
        public string Endpoint { get; }
        public string TransportAddress { get; }        
        public Subscriber(string endpoint, string transportAddress)
        {
            Endpoint = endpoint;
            TransportAddress = transportAddress;
        }
    }
}