namespace NServiceBus.SqlServer.AcceptanceTests.LegacyMultiInstance
{
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_doing_request_reply_between_modes : NServiceBusAcceptanceTest
    {
        static string MultiCatalogEndpointConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True";
        static string MultiInstanceEndpointConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True";

        static string MultiCatalogEndpointName => Conventions.EndpointNamingConvention(typeof(MultiCatalogEndpoint));
        static string MultiInstanceEndpointName => Conventions.EndpointNamingConvention(typeof(MultiInstanceEndpoint));

        [Test]
        public Task Should_be_able_to_send_and_receive_messages()
        {
            return Scenario.Define<Context>()
                .WithEndpoint<MultiCatalogEndpoint>(c => c.When(x => x.EndpointsStarted,  s => s.Send(new MultiCatalogRequest())))
                .WithEndpoint<MultiInstanceEndpoint>(c => c.When(x => x.EndpointsStarted, s => s.Send(new MultiInstanceRequest())))
                .Done(c => c.MultiCatalogReplyReceived && c.MultiInstanceReplyReceived)
                .Run();
        }

        public class MultiCatalogEndpoint : EndpointConfigurationBuilder
        {
            public MultiCatalogEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<SqlServerTransport>()
                        .ConnectionString(MultiCatalogEndpointConnectionString)
                        .UseCatalogForEndpoint(MultiInstanceEndpointName, "nservicebus2")
                        .UseCatalogForQueue(MultiInstanceEndpointName, "nservicebus2")
                        .Routing();

                    routing.RouteToEndpoint(typeof(MultiCatalogRequest), MultiInstanceEndpointName);
                });
            }


            class RequestHandler : IHandleMessages<MultiInstanceRequest>
            {
                public Context Context { get; set; }

                public Task Handle(MultiInstanceRequest message, IMessageHandlerContext context)
                {
                    return context.Reply(new MultiCatalogResponse());
                }
            }

            class ResponseHandler : IHandleMessages<MultiInstanceReponse>
            {
                public Context Context { get; set; }

                public Task Handle(MultiInstanceReponse message, IMessageHandlerContext context)
                {
                    Context.MultiInstanceReplyReceived = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MultiInstanceEndpoint : EndpointConfigurationBuilder
        {
            public MultiInstanceEndpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
#pragma warning disable 0618
                    var routing = c.UseTransport<SqlServerTransport>()
                        .EnableLegacyMultiInstanceMode(async queueName =>
                        {
                            var connectionString = queueName.Contains("MultiCatalog") ? MultiCatalogEndpointConnectionString : MultiInstanceEndpointConnectionString;
                            var connection = new SqlConnection(connectionString);

                            await connection.OpenAsync();

                            return connection;
                        })
                        .Routing();
#pragma warning restore 0618
                    routing.RouteToEndpoint(typeof(MultiInstanceRequest), MultiCatalogEndpointName);
                });
            }

            class RequestHandler : IHandleMessages<MultiCatalogRequest>
            {
                public Task Handle(MultiCatalogRequest message, IMessageHandlerContext context)
                {
                    return context.Reply(new MultiInstanceReponse());
                }
            }

            class ResponseHandler : IHandleMessages<MultiCatalogResponse>
            {
                public Context Context { get; set; }

                public Task Handle(MultiCatalogResponse message, IMessageHandlerContext context)
                {
                    Context.MultiCatalogReplyReceived = true;
                    return Task.FromResult(0);
                }
            }
        }

        public class MultiCatalogRequest : ICommand
        {
        }

        public class MultiCatalogResponse : IMessage
        {
        }

        public class MultiInstanceReponse : IMessage
        {
        }

        public class MultiInstanceRequest : ICommand
        {
        }

        class Context : ScenarioContext
        {
            public bool MultiCatalogReplyReceived { get; set; }
            public bool MultiInstanceReplyReceived { get; set; }
        }
    }
}