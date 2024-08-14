namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Faults;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_deferring_a_message_to_invalid_queue : NServiceBusAcceptanceTest
    {
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.None)]
        public async Task Should_forward_it_to_the_error_queue(TransportTransactionMode transactionMode)
        {
            if (transactionMode == TransportTransactionMode.TransactionScope && !OperatingSystem.IsWindows())
            {
                Assert.Ignore("Transaction scope mode is only supported on windows");
            }

            var delay = TimeSpan.FromSeconds(5); //To make sure it is actually stored

            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.DoNotFailOnErrorMessages()
                    .CustomConfig(c =>
                    {
                        c.ConfigureSqlServerTransport().TransportTransactionMode = transactionMode;
                    })
                    .When((session, c) =>
                    {
                        var options = new SendOptions();

                        options.DelayDeliveryWith(delay);
                        options.SetDestination("InvalidDestination__");

                        return session.Send(new MyMessage(), options);
                    }))
                .WithEndpoint<ErrorSpy>()
                .Done(c => c.MessageForwardedToErrorQueue)
                .Run();

            Assert.That(context.MessageForwardedToErrorQueue, Is.True);
            Assert.That(context.Headers[FaultsHeaderKeys.FailedQ], Is.EqualTo("InvalidDestination__"));
        }

        public class Context : ScenarioContext
        {
            public bool MessageForwardedToErrorQueue { get; set; }
            public IReadOnlyDictionary<string, string> Headers { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    config.SendFailedMessagesTo(Conventions.EndpointNamingConvention(typeof(ErrorSpy)));
                });
            }
        }

        public class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy()
            {
                EndpointSetup<DefaultServer>();
            }

            class Handler : IHandleMessages<MyMessage>
            {
                readonly Context scenarioContext;
                public Handler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.Headers = context.MessageHeaders;
                    scenarioContext.MessageForwardedToErrorQueue = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}