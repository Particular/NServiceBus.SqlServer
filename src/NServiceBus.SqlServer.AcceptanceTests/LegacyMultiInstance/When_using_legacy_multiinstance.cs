namespace NServiceBus.SqlServer.AcceptanceTests.LegacyMultiInstance
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using Transport.SQLServer;

    public abstract class When_using_legacy_multiinstance : NServiceBusAcceptanceTest
    {
        protected static string SenderConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True";
        static string ReceiverConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True";

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.OverridePublicReturnAddress($"{Conventions.EndpointNamingConvention(typeof(Sender))}@[]@nservicebus1");
#pragma warning disable 0618
                    c.UseTransport<SqlServerTransport>()
                        .ConnectionString("should-not-be-used")
                        .UseSchemaForEndpoint("Receiver", "receiver")
                        .UseSchemaForEndpoint("Sender", "sender")
                        .EnableLegacyMultiInstanceMode(async queueName =>
                        {
                            var connectionString = queueName.Contains("Receiver") ? ReceiverConnectionString : SenderConnectionString;
                            var connection = new SqlConnection(connectionString);

                            await connection.OpenAsync();

                            return connection;
                        });
#pragma warning restore 0618
                }).AddMapping<Message>(typeof(Receiver));
            }

            class Handler : IHandleMessages<Reply>
            {
                public Context Context { get; set; }

                public Task Handle(Reply message, IMessageHandlerContext context)
                {
                    Context.ReplyReceived = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.OverridePublicReturnAddress($"{Conventions.EndpointNamingConvention(typeof(Receiver))}@[]@nservicebus2");
#pragma warning disable 0618
                    c.UseTransport<SqlServerTransport>()
                        .ConnectionString("should-not-be-used")
                        .UseSchemaForEndpoint("Receiver", "receiver")
                        .UseSchemaForEndpoint("Sender", "sender")
                        .EnableLegacyMultiInstanceMode(async address =>
                        {
                            var connectionString = address.Contains("Sender") ? SenderConnectionString : ReceiverConnectionString;
                            var connection = new SqlConnection(connectionString);

                            await connection.OpenAsync();

                            return connection;
                        });
#pragma warning restore 0618
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    return context.Reply(new Reply());
                }
            }
        }

        public class Message : ICommand
        {
        }

        public class Reply : IMessage
        {
        }

        protected class Context : ScenarioContext
        {
            public bool ReplyReceived { get; set; }
        }
    }
}