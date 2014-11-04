namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NServiceBus.Config;
    using NServiceBus.Config.ConfigurationSource;
    using NUnit.Framework;

    public class When_using_non_standard_schema : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_receive_the_message()
        {
            Scenario.Define(() => new Context { Id = Guid.NewGuid() })
                    .WithEndpoint<Sender>(b => b.Given((bus, context) => bus.Send<MyMessage>(m=>
                    {
                        m.Id = context.Id;
                    })))
                    .WithEndpoint<Receiver>()
                    .Done(c => c.WasCalled)
                    .Repeat(r =>r.For(Serializers.Binary))
                    .Should(c => Assert.True(c.WasCalled, "The message handler should be called"))
                    .Run();
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
                EndpointSetup<DefaultServer>(x => x.UseTransport<SqlServerTransport>().UseSchema("sender").EnableSchemaAwareAddressing(true));
            }

            public class ConfigureMapping : IProvideConfiguration<UnicastBusConfig>
            {
                public UnicastBusConfig GetConfiguration()
                {
                    return new UnicastBusConfig()
                    {
                        MessageEndpointMappings = new MessageEndpointMappingCollection()
                        {
                            new MessageEndpointMapping()
                            {
                                AssemblyName = typeof(MyMessage).Assembly.FullName,
                                TypeFullName = typeof(MyMessage).FullName,
                                Endpoint = "receiver.Basic.Receiver.WhenUsingNonStandardSchema.Binary"
                            }
                        }
                    };
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(x => x.UseTransport<SqlServerTransport>().UseSchema("receiver"));
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
