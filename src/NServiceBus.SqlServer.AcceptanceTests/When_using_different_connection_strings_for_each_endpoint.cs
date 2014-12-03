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
        const string ClientConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus1;Trusted_Connection=True;";
        const string ServerConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus2;Trusted_Connection=True;";

        [Test]
        public void Should_use_configured_connection_string_when_replying()
        {

            var context = new Context()
            {
                Id = Guid.NewGuid()
            };

            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => AddConnectionString("NServiceBus/Transport/Basic.Sender.WhenUsingDifferentConnectionStringsForEachEndpoint.SqlServerTransport", ClientConnectionString)))
                   .WithEndpoint<Sender>(b => b.Given((bus, c) => bus.Send(new MyRequest
                   {
                       ContextId = c.Id
                   })))
                   .Done(c => context.GotResponse)
                   .Run();
        }

        static void AddConnectionString(string name, string value)
        {
            var connectionStrings = ConfigurationManager.ConnectionStrings;
            //Setting the read only field to false via reflection in order to modify the connection strings
            var readOnlyField = typeof(ConfigurationElementCollection).GetField("bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            readOnlyField.SetValue(connectionStrings, false);
            connectionStrings.Add(new ConnectionStringSettings(name, value));
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
                        //Use programmatic configuration
                        .UseDifferentConnectionStringsForEndpoints(x => x == "Basic.Receiver.WhenUsingDifferentConnectionStringsForEachEndpoint.SqlServerTransport" ? ServerConnectionString : null)
                        .ConnectionString(ClientConnectionString);
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
                        .UseDifferentConnectionStringsForEndpoints(
                            new EndpointConnectionString("Basic.Sender.WhenUsingDifferentConnectionStringsForEachEndpoint.SqlServerTransport","ToBeOverridenViaConfig"))
                        .ConnectionString(ServerConnectionString);
                    busConfiguration.Transactions().DisableDistributedTransactions();
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