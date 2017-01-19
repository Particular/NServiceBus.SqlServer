﻿namespace NServiceBus.SqlServer.AcceptanceTests.MultiSchema
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_custom_schema_configured_for_error_queue : NServiceBusAcceptanceTest
    {
        [Test]
        public Task Error_should_be_sent_to_table_in_configured_schema()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<Sender>(b =>
                {
                    b.DoNotFailOnErrorMessages();
                    b.When((bus, c) => bus.SendLocal(new Message()));
                })
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.FailedMessageProcessed)
                .Repeat(r => r.For(NServiceBus.AcceptanceTests.ScenarioDescriptors.Transports.Default))
                .Should(c => Assert.True(c.FailedMessageProcessed, "Message should be moved to error queue in custom schema"))
                .Run();
        }

        public class Context : ScenarioContext
        {
            public bool FailedMessageProcessed { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var errorSpyName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy));

                    c.SendFailedMessagesTo(errorSpyName);

                    c.Recoverability()
                        .Immediate(i => i.NumberOfRetries(0))
                        .Delayed(d => d.NumberOfRetries(0));

                    c.UseTransport<SqlServerTransport>().UseSchemaForQueue(errorSpyName, ErrorSpySchema);
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServerTransport>().DefaultSchema(ErrorSpySchema);
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.FailedMessageProcessed = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Message : ICommand { }

        const string ErrorSpySchema = "receiver";
    }
}