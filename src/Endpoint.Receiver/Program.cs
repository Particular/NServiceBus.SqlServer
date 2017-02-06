namespace Endpoint.Receiver
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
            var endpointConfiguration = new EndpointConfiguration("Endpoint.Receiver");

            endpointConfiguration.UseTransport<SqlServerTransport>()
                                 .ConnectionString(ConnectionString);

            endpointConfiguration.UsePersistence<InMemoryPersistence>();

            endpointConfiguration.ForwardReceivedMessagesTo("error");

            endpointConfiguration.Recoverability()
                                 .Immediate(i => i.NumberOfRetries(1))
                                 .Delayed(d => d.NumberOfRetries(0));

            var endpoint = Endpoint.Start(endpointConfiguration);

            Console.WriteLine("Press any <key> to exit ...");
            Console.ReadKey();
        }

        public class FailingHandler : IHandleMessages<FailingMessage>
        {
            public Task Handle(FailingMessage message, IMessageHandlerContext context)
            {
                throw new NotImplementedException();
            }
        }

        static string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
    }
}
