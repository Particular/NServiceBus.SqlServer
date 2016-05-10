﻿namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using Transport.SQLServer;

    public class When_custom_schema_configured : NServiceBusAcceptanceTest
    {
        public const string ReceiverSchema = "receiver";

        public class Context : ScenarioContext
        {
            public bool MessageReceived { get; set; }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c => { c.UseTransport<SqlServerTransport>().DefaultSchema(ReceiverSchema); });
            }

            class Handler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
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