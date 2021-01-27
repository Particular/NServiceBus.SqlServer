namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_computed_message_body_column : NServiceBusAcceptanceTest
    {
        [Test]
        public async Task Simple_send_is_received()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((session, c) => session.SendLocal(new MyMessage())))
                .Done(c => c.WasCalled)
                .Run();

            Assert.IsTrue(context.WasCalled);
        }
        class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var transportConfig = config.ConfigureSqlServerTransport();
                    transportConfig.CreateMessageBodyComputedColumn = true;
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                readonly Context scenarioContext;
                public MyMessageHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.WasCalled = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MyMessage : IMessage
        {
        }
    }
}
