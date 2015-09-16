namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    public class When_using_non_standard_schema : NServiceBusAcceptanceTest
    {
        
        [Test]
        public void Should_receive_the_message()
        {
            var context = new Context
            {
                Id = Guid.NewGuid()
            };

            Scenario.Define(context)
                    .WithEndpoint<Sender>(b => b.Given((bus, ctx) => bus.Send<MyMessage>(m => { m.Id = ctx.Id; })))
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
                EndpointSetup<DefaultServer>(x => x
                    .UseTransport<SqlServerTransport>()
                    .DefaultSchema("sender")
                    .UseSpecificConnectionInformation(
                        EndpointConnectionInfo.For("UsingNonStandardSchema.Receiver").UseSchema("receiver")
                    ))
                    .AddMapping<MyMessage>(typeof(Receiver));
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(x => x
                    .UseTransport<SqlServerTransport>()
                    .DefaultSchema("WillBeOverriddenViaConnectionString")
                    .ConnectionString(@"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;Queue Schema=receiver"));
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