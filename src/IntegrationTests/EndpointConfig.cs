namespace SqlServer.IntegrationTests
{
    using NServiceBus;

    public class EndpointConfig:IConfigureThisEndpoint,AsA_Publisher,UsingTransport<SqlServer>
    {
    }

    class Starter:IWantToRunWhenBusStartsAndStops
    {
        public IBus Bus { get; set; }
        public void Start()
        {
            Bus.SendLocal(new StartSagaMessage());
        }

        public void Stop()
        {
            
        }
    }
}
