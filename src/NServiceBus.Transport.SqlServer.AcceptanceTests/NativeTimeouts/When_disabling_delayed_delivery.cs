namespace NServiceBus.Transport.SqlServer.AcceptanceTests.NativeTimeouts
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTesting.Customization;
    using NUnit.Framework;

    public class When_disabling_delayed_delivery : NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetUpConnectionString()
        {
            connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";
            }
        }

        [Test]
        public async Task Should_not_create_delayed_queue()
        {
            var context = await Scenario.Define<Context>()
                .WithEndpoint<Endpoint>(b => b.When((session, c) =>
                {
                    return session.SendLocal(new MyMessage());
                }))
                .Done(c => c.WasCalled && DelayedQueueIsNotCreated())
                .Run();
        }

        bool DelayedQueueIsNotCreated()
        {
            var endpoint = Conventions.EndpointNamingConvention(typeof(Endpoint));
            // TODO: Move opening SQL connection out of the method.
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand($"SELECT COUNT(*) FROM sys.objects WHERE name = '{endpoint}.Delayed'", connection))
                {
                    var numberOfMessagesInQueue = (int)command.ExecuteScalar();
                    return numberOfMessagesInQueue == 0;
                }
            }
        }

        public class Context : ScenarioContext
        {
            public bool WasCalled { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(config =>
                {
                    var transport = config.UseTransport<SqlServerTransport>();
                    transport.DisableDelayedDelivery();
                });
            }

            public class MyMessageHandler : IHandleMessages<MyMessage>
            {
                private readonly Context scenarioContext;
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

        string connectionString;
    }
}