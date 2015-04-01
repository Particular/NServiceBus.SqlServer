namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using System.Configuration;
    using System.Reflection;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    public class When_using_different_connection_strings_for_each_endpoint : NServiceBusAcceptanceTest
    {
        const string SenderConnectionStringWithSchema = @"Server=localhost\sqlexpress;Database=nservicebus1;Trusted_Connection=True;Queue Schema=nsb";
        const string ReceiverConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus2;Trusted_Connection=True;";
        const string ReceiverConnectionStringWithSchema = @"Server=localhost\sqlexpress;Database=nservicebus2;Trusted_Connection=True;Queue Schema=nsb";

        [Test]
        public void Should_use_configured_connection_string_when_replying()
        {
            var context = new Context()
            {
                Id = Guid.NewGuid()
            };

            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => AddConnectionString("NServiceBus/Transport/UsingDifferentConnectionStringsForEachEndpoint.Sender", SenderConnectionStringWithSchema)))
                   .WithEndpoint<Sender>(b => b.Given((bus, c) => bus.Send(new MyRequest
                   {
                       ContextId = c.Id
                   })))
                   .Done(c => context.GotResponse)
                   .Run();
        }

        [SetUp]
        [TearDown]
        public void ClearConnectionStrings()
        {
            var connectionStrings = ConfigurationManager.ConnectionStrings;
            //Setting the read only field to false via reflection in order to modify the connection strings
            var readOnlyField = typeof(ConfigurationElementCollection).GetField("bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            readOnlyField.SetValue(connectionStrings, false);
            connectionStrings.Clear();
        }

        static void AddConnectionString(string name, string value)
        {
            ConfigurationManager.ConnectionStrings.Add(new ConnectionStringSettings(name, value));
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>()
                    .AddMapping<MyRequest>(typeof(Receiver));
            }

            public class ConfigureTransport
            {
                public void Configure(BusConfiguration busConfiguration)
                {
                    busConfiguration.UseTransport<SqlServerTransport>()
                        .UseSpecificConnectionInformation(x => x == "UsingDifferentConnectionStringsForEachEndpoint.Receiver" ? ConnectionInfo.Create().UseConnectionString(ReceiverConnectionString).UseSchema("nsb") : null)
                        .ConnectionString(SenderConnectionStringWithSchema);
                }

                public void Cleanup()
                {
                }
            }

            class MyReplyHandler : IHandleMessages<MyReply>
            {
                public IBus Bus { get; set; }
                public Context Context { get; set; }

                public void Handle(MyReply message)
                {
                    if (Context.Id != message.ContextId)
                    {
                        return;
                    }
                    Context.GotResponse = true;
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>();
            }

            public class ConfigureTransport
            {
                public void Configure(BusConfiguration busConfiguration)
                {
                    busConfiguration.UseTransport<SqlServerTransport>()
                        .UseSpecificConnectionInformation(
                            EndpointConnectionInfo.For("Basic.Sender.WhenUsingDifferentConnectionStringsForEachEndpoint.SqlServerTransport").UseConnectionString("ToBeOverridenViaConfig").UseSchema("ToBeOverridenViaConfig"))
                        .ConnectionString(ReceiverConnectionStringWithSchema);
                }

                public void Cleanup()
                {
                }
            }

            class MyEventHandler : IHandleMessages<MyRequest>
            {
                public IBus Bus { get; set; }
                public Context Context { get; set; }

                public void Handle(MyRequest message)
                {
                    if (Context.Id != message.ContextId)
                    {
                        return;
                    }
                    Bus.Reply(new MyReply
                    {
                        ContextId = message.ContextId
                    });
                }
            }
        }

        class MyRequest : IMessage
        {
            public Guid ContextId { get; set; }
        }

        class MyReply : IMessage
        {
            public Guid ContextId { get; set; }
        }

        class Context : ScenarioContext
        {
            public bool GotResponse { get; set; }
            public Guid Id { get; set; }
        }
    }
}