namespace NServiceBus.Transport.SqlServer.AcceptanceTests;

using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using AcceptanceTesting.Support;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_ReplyTo_address_does_not_exist : NServiceBusAcceptanceTest
{
    [Test]
    public void Should_throw()
    {
        var exception = Assert.ThrowsAsync<MessageFailedException>(async () =>
        {
            await Scenario.Define<Context>()
                .WithEndpoint<Attacker>(b => b.When(session => session.SendLocal(new StartCommand())))
                .WithEndpoint<Victim>()
                .Run();
        });

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception.FailedMessage.Exception.Message, Contains.Substring("Failed to send message to"));
    }

    class Context : ScenarioContext;

    class Attacker : EndpointConfigurationBuilder
    {
        public Attacker() =>
            EndpointSetup<DefaultServer>(b =>
            {
                b.OverridePublicReturnAddress("error] VALUES(NEWID(), NULL, NULL, 1, NULL, '', NULL); DROP TABLE [Victim]; INSERT INTO [error");
                var routing = b.ConfigureRouting();
                routing.RouteToEndpoint(typeof(AttackCommand), Conventions.EndpointNamingConvention(typeof(Victim)));
            });

        class StartHandler : IHandleMessages<StartCommand>
        {
            public Task Handle(StartCommand message, IMessageHandlerContext context) => context.Send(new AttackCommand());
        }

        class AttackResponseHandler : IHandleMessages<AttackResponse>
        {
            public Task Handle(AttackResponse message, IMessageHandlerContext context) => Task.CompletedTask;
        }
    }

    class Victim : EndpointConfigurationBuilder
    {
        public Victim() => EndpointSetup<DefaultServer>();

        class AttackHandler : IHandleMessages<AttackCommand>
        {
            public Task Handle(AttackCommand message, IMessageHandlerContext context) => context.Reply(new AttackResponse());
        }
    }

    public class StartCommand : ICommand;

    public class AttackCommand : ICommand;

    public class AttackResponse : IMessage;
}