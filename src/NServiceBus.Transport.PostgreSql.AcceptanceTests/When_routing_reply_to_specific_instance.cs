﻿namespace NServiceBus.AcceptanceTests.Routing
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Configuration.AdvancedExtensibility;
    using EndpointTemplates;
    using NUnit.Framework;

    [TestFixture]
    class When_routing_reply_to_specific_instance : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Should_route_reply_to_instance_specific_queue()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(e => e
                    .When(s =>
                    {
                        var options = new SendOptions();
                        options.RouteReplyToThisInstance();
                        return s.Send(new RequestReplyMessage(), options);
                    }))
                .WithEndpoint<Replier>()
                .Done(c => c.ReplyReceived)
                .Run();

            Assert.That(context.ReplyReceived, Is.True);
            Assert.That(context.ReplyToAddress, Does.Contain(instanceDiscriminator));
        }

        const string instanceDiscriminator = "instance-42";

        class Context : ScenarioContext
        {
            public string ReplyToAddress { get; set; }
            public bool ReplyReceived { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    //HINT: we need to make sure that the table name (including discriminator) is not exceeding the max length of 55 characters
                    c.GetSettings().Set("NServiceBus.Routing.EndpointName", "Short_endpoint_name");
                    c.MakeInstanceUniquelyAddressable(instanceDiscriminator);
                    c.ConfigureRouting().RouteToEndpoint(typeof(RequestReplyMessage), typeof(Replier));
                });
            }

            class ReplyMessageHandler : IHandleMessages<ReplyMessage>
            {
                public ReplyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(ReplyMessage message, IMessageHandlerContext context)
                {
                    testContext.ReplyReceived = true;
                    return Task.CompletedTask;
                }

                Context testContext;
            }
        }

        class Replier : EndpointConfigurationBuilder
        {
            public Replier()
            {
                EndpointSetup<DefaultServer>();
            }

            class RequestReplyMessageHandler : IHandleMessages<RequestReplyMessage>
            {
                public RequestReplyMessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(RequestReplyMessage message, IMessageHandlerContext context)
                {
                    testContext.ReplyToAddress = context.ReplyToAddress;
                    return context.Reply(new ReplyMessage());
                }

                Context testContext;
            }
        }

        public class RequestReplyMessage : ICommand
        {
        }

        public class ReplyMessage : IMessage
        {
        }
    }
}