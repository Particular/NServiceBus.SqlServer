namespace NServiceBus.SqlServer.AcceptanceTests.MultiInstance
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;

    public abstract class When_instance_configured : NServiceBusAcceptanceTest
    {
        public static string SpyAddress => Conventions.EndpointNamingConvention(typeof(Spy));

        public class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
            public IReadOnlyDictionary<string, string> Headers { get; set; }
        }

        public class Spy : EndpointConfigurationBuilder
        {
            public Spy()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.Headers = context.MessageHeaders;
                    Context.MessageReceived = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Message : IMessage
        {
        }
    }
}