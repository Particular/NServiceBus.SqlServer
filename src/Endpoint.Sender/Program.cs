namespace Endpoint.Sender
{
    using System;
    using System.Threading.Tasks;
    using NServiceBus;
    using Shared;

    class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            var configuraiton = new EndpointConfiguration("Endpoint.Sender");
            configuraiton.UseTransport<SqlServerTransport>()
                         .ConnectionString(ConnectionString)
                         .Routing().RouteToEndpoint(typeof(FailingMessage), "Endpoint.Receiver");

            configuraiton.UsePersistence<InMemoryPersistence>();

            configuraiton.ForwardReceivedMessagesTo("error");

            var endpoint = await Endpoint.Start(configuraiton);

            while (true)
            {
                Console.WriteLine("Press any <key> to send message ...");
                Console.ReadKey()
                    ;
                await endpoint.Send(new FailingMessage());

            }
        }

        const string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
    }

}
