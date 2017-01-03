namespace NServiceBus.SqlServer.AcceptanceTests.MultiInstance
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_using_multiinstance : NServiceBusAcceptanceTest
    {
        static string SenderConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True";
        static string ReceiverConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True";
        static string OutgoingQueueSpyAddress => Conventions.EndpointNamingConvention(typeof(OutgoingQueueSpy));
        static string ReceiverAddress => Conventions.EndpointNamingConvention(typeof(Receiver));

        [Test]
        public async Task Should_be_able_to_send_message_to_input_queue_in_different_database()
        {
            var ctx = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new Message())))
                .WithEndpoint<Receiver>()
                .WithEndpoint<OutgoingQueueSpy>()
                .Done(c => c.MessageDetectedInOutgoingQueue)
                .Run();

            StringAssert.Contains(ReceiverAddress, ctx.Destination);
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<SqlServerTransport>()
                        .UseCatalogForEndpoint(ReceiverAddress, "nservicebus2")
                        .UseOutgoingQueueForCatalog("nservicebus2", OutgoingQueueSpyAddress)
                        .ConnectionString(SenderConnectionString)
                        .Routing();

                    routing.RouteToEndpoint(typeof(Message), ReceiverAddress);
                });
            }

        }

        public class OutgoingQueueSpy : EndpointConfigurationBuilder
        {
            public OutgoingQueueSpy()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServerTransport>().ConnectionString(SenderConnectionString);
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.Destination = context.MessageHeaders["NServiceBus.SqlServer.Destination"];
                    Context.MessageDetectedInOutgoingQueue = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServerTransport>().ConnectionString(ReceiverConnectionString);
                });
            }
        }

        protected class Message : ICommand
        {
        }
        
        protected class Context : ScenarioContext
        {
            public bool MessageDetectedInOutgoingQueue { get; set; }
            public string Destination { get; set; }
        }
    }
}