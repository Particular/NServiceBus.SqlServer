namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_sending_to_non_standard_schema : NServiceBusAcceptanceTest
    {
        
        [Test]
        public void Should_receive_the_message()
        {
            var context = new Context
            {
                Id = Guid.NewGuid()
            };

            Scenario.Define(context)
                    .WithEndpoint<Sender>(b => b.Given((bus, ctx) =>
                    {
                        var destinationQueue = Conventions.EndpointNamingConvention(typeof(Receiver));
                        var address = $"{destinationQueue}@[receiver]";
                        bus.Send<MyMessage>(address, m => { m.Id = ctx.Id; });
                    }))
                    .WithEndpoint<Receiver>()
                    .Done(c => c.WasCalled)
                    .Run();

            Assert.True(context.WasCalled, "The message handler should be called");
        }

        
        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
            public Guid Id { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(x => x.UseTransport<SqlServerTransport>());
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(x => x
                    .UseTransport<SqlServerTransport>()
                    .DefaultSchema("receiver"));
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                public Context Context { get; set; }

                public IBus Bus { get; set; }

                public void Handle(MyMessage message)
                {
                    if (Context.Id != message.Id)
                    {
                        return;
                    }
                    Context.WasCalled = true;
                }
            }
        }

        [Serializable]
        public class MyMessage : ICommand
        {
            public Guid Id { get; set; }
        }
    }
}