namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using System.Configuration;
    using System.Data;
    using System.Data.SqlClient;
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
            EnsureSchemaExists("nsb");
            var context = new Context()
            {
                Id = Guid.NewGuid()
            };

            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => AddConnectionString("NServiceBus/Transport/Basic.Sender.WhenUsingDifferentConnectionStringsForEachEndpoint.SqlServerTransport", SenderConnectionStringWithSchema)))
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
                        .UseSpecificConnectionInformation(x => x == "Basic.Receiver.WhenUsingDifferentConnectionStringsForEachEndpoint.SqlServerTransport" ? ConnectionInfo.Create().UseConnectionString(ReceiverConnectionString).UseSchema("nsb") : null)
                        .ConnectionString(SenderConnectionStringWithSchema);
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

        static void EnsureSchemaExists(string schema)
        {
            const string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
            const string SchemaDdl =
    @"IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{0}')
BEGIN
    EXEC('CREATE SCHEMA {0}');
END";

            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(string.Format(SchemaDdl, schema), conn)
                {
                    CommandType = CommandType.Text
                })
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}