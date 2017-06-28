namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System.Linq;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Support;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_ReplyTo_address_does_not_exist : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
        {
            var exception = Assert.ThrowsAsync<MessagesFailedException>(async () =>
                await Scenario.Define<Context>()
                    .WithEndpoint<Attacker>(b => b.When(session => session.SendLocal(new StartCommand())))
                    .WithEndpoint<Victim>()
                    .Done(c => c.FailedMessages.Any())
                    .Run());

            Assert.That(exception.FailedMessages, Has.Count.EqualTo(1));

            var failedMessage = exception.FailedMessages.Single();

            Assert.That(failedMessage.Exception.Message, Contains.Substring("Failed to send message to"));
        }

        class Context : ScenarioContext
        {
        }

        class Attacker : EndpointConfigurationBuilder
        {
            public Attacker()
            {
                EndpointSetup<DefaultServer>(b => b.OverridePublicReturnAddress("error] VALUES(NEWID(), NULL, NULL, 1, NULL, '', NULL); DROP TABLE [Victim]; INSERT INTO [error"))
                    .AddMapping<AttackCommand>(typeof(Victim));
            }

            class StartHandler : IHandleMessages<StartCommand>
            {
                public Task Handle(StartCommand message, IMessageHandlerContext context)
                {
                    return context.Send(new AttackCommand());
                }
            }

            class AttackResponseHandler : IHandleMessages<AttackResponse>
            {
                public Task Handle(AttackResponse message, IMessageHandlerContext context)
                {
                    return Task.FromResult(0);
                }
            }
        }

        class Victim : EndpointConfigurationBuilder
        {
            public Victim()
            {
                EndpointSetup<DefaultServer>();
            }

            class AttackHandler : IHandleMessages<AttackCommand>
            {
                public Task Handle(AttackCommand message, IMessageHandlerContext context)
                {
                    return context.Reply(new AttackResponse());
                }
            }
        }

        public class StartCommand : ICommand
        {
        }

        public class AttackCommand : ICommand
        {
        }

        public class AttackResponse : IMessage
        {
        }
    }
}