namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Config;
    using NServiceBus.Faults;
    using NServiceBus.Features;
    using NUnit.Framework;

    public class When_ReplyTo_address_does_not_exist
    {
        [Test]
        public void Should_throw()
        {
            var context = Scenario.Define<Context>()
                .WithEndpoint<Attacker>(b => b.When(bus => bus.SendLocal(new StartCommand())))
                .WithEndpoint<Victim>()
                .AllowExceptions()
                .Done(c => c.ExceptionReceived)
                .Run();

            Assert.That(context.ExceptionMessage, Contains.Substring("The destination queue") & Contains.Substring("could not be found"));
            Assert.That(context.ExceptionMessage, Contains.Substring("error] VALUES(NEWID(), NULL, NULL, 1, NULL, '', NULL); DROP TABLE [Victim]; INSERT INTO [error"));
        }

        class Context : ScenarioContext
        {
            public bool ExceptionReceived { get; set; }
            public string ExceptionMessage { get; set; }
        }

        class Attacker : EndpointConfigurationBuilder
        {
            public Attacker()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServerTransport>()
                        .DisableCallbackReceiver();
                    c.OverridePublicReturnAddress(Address.Parse("error] VALUES(NEWID(), NULL, NULL, 1, NULL, '', NULL); DROP TABLE [Victim]; INSERT INTO [error"));
                })
                    .AddMapping<AttackCommand>(typeof(Victim));
            }

            class StartHandler : IHandleMessages<StartCommand>
            {
                public IBus Bus { get; set; }

                public void Handle(StartCommand message)
                {
                    Bus.Send(new AttackCommand());
                }
            }

            class AttackResponseHandler : IHandleMessages<AttackResponse>
            {
                public void Handle(AttackResponse message)
                {
                }
            }
        }

        class Victim : EndpointConfigurationBuilder
        {
            public Victim()
            {
                EndpointSetup<DefaultServer>(b =>
                {
                    b.RegisterComponents(c => c.ConfigureComponent<CustomFaultManager>(DependencyLifecycle.SingleInstance));
                    b.DisableFeature<TimeoutManager>();
                })
                    .WithConfig<TransportConfig>(c => c.MaxRetries = 0);
            }

            class AttackHandler : IHandleMessages<AttackCommand>
            {
                public IBus Bus { get; set; }

                public void Handle(AttackCommand message)
                {
                    Bus.Reply(new AttackResponse());
                }
            }

            class CustomFaultManager : IManageMessageFailures
            {
                public Context Context { get; set; }

                public void SerializationFailedForMessage(TransportMessage message, Exception e)
                {
                }

                public void ProcessingAlwaysFailsForMessage(TransportMessage message, Exception e)
                {
                    Context.ExceptionMessage = e.Message;
                    Context.ExceptionReceived = true;
                }

                public void Init(Address address)
                {
                }
            }
        }

        class StartCommand : ICommand
        {
        }

        class AttackCommand : ICommand
        {
        }

        class AttackResponse : IMessage
        {
        }
    }
}