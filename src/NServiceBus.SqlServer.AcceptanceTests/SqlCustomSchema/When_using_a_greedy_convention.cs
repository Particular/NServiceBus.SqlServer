namespace NServiceBus.AcceptanceTests.SqlCustomSchema
{
    using System;
    using EndpointTemplates;
    using AcceptanceTesting;
    using NUnit.Framework;

    public class When_using_a_greedy_convention : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_receive_the_message()
        {
            var scenarioContext = new Context
            {
                Id = Guid.NewGuid()
            };
            Scenario.Define( scenarioContext )
                    .WithEndpoint<EndPoint>(b => b.Given((bus, context) => bus.SendLocal(new MyMessage
                    {
                        Id = context.Id
                    })))
                    .Done(c => c.WasCalled)
                    .Run();

            Assert.True( scenarioContext.WasCalled, "The message handler should be called" );
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }

            public Guid Id { get; set; }
        }

        public class EndPoint : EndpointConfigurationBuilder
        {
            public EndPoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServer>(ConnectionStringWith_test_schema);
                    c.DefiningMessagesAs(MessageConvention);
                });
            }

            static bool MessageConvention(Type t)
            {
                return t.Namespace != null && 
                    (t.Namespace.EndsWith(".Messages") ||  (t == typeof(MyMessage)));
            }
        }

        [Serializable]
        public class MyMessage
        {
            public Guid Id { get; set; }
        }

        public class MyMessageHandler : IHandleMessages<MyMessage>
        {
            public Context Context { get; set; }
            public void Handle(MyMessage message)
            {
                if (Context.Id != message.Id)
                    return;

                Context.WasCalled = true;
            }
        }
    }
}
