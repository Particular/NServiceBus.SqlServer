namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_the_message_contains_a_legacy_callback_header : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task It_should_reply_to_an_address_sent_in_that_header()
        {
            var context = await Scenario.Define<MyContext>()
                .WithEndpoint<OriginatingEndpoint>(c => c.When(bus =>
                {
                    var options = new SendOptions();
                    options.SetHeader("NServiceBus.SqlServer.CallbackQueue", Conventions.EndpointNamingConvention(typeof(SpyEndpoint)));
                    return bus.Send(new Request(), options);
                }))
                .WithEndpoint<ReceivingEndpoint>(b => b.DoNotFailOnErrorMessages())
                .WithEndpoint<SpyEndpoint>()
                .Done(c => c.Done)
                .Run(TimeSpan.FromMinutes(1));

            Assert.IsFalse(context.RepliedToWrongQueue);
            Assert.IsTrue(context.RepliedToCorrectQueue);
        }

        public class Request : IMessage
        {
        }

        public class Reply : IMessage
        {
        }

        class MyContext : ScenarioContext
        {
            public bool Done { get; set; }
            public bool RepliedToWrongQueue { get; set; }
            public bool RepliedToCorrectQueue { get; set; }
        }

        class OriginatingEndpoint : EndpointConfigurationBuilder
        {
            public OriginatingEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.ConfigureRouting();
                    routing.RouteToEndpoint(typeof(Request), Conventions.EndpointNamingConvention(typeof(ReceivingEndpoint)));
                });
            }

            class ReplyHandler : IHandleMessages<Reply>
            {
                readonly MyContext scenarioContext;
                public ReplyHandler(MyContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    scenarioContext.RepliedToWrongQueue = true;
                    scenarioContext.Done = true;
                    return Task.FromResult(0);
                }
            }
        }

        class SpyEndpoint : EndpointConfigurationBuilder
        {
            public SpyEndpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class ReplyHandler : IHandleMessages<Reply>
            {
                readonly MyContext scenarioContext;
                public ReplyHandler(MyContext scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    scenarioContext.RepliedToCorrectQueue = true;
                    scenarioContext.Done = true;
                    return Task.FromResult(0);
                }
            }
        }

        class ReceivingEndpoint : EndpointConfigurationBuilder
        {
            public ReceivingEndpoint()
            {
                EndpointSetup<DefaultServer>(c => { });
            }

            public class RequestHandler : IHandleMessages<Request>
            {
                public Task Handle(Request message, IMessageHandlerContext context)
                {
                    return context.Reply(new Reply());
                }
            }
        }
    }
}