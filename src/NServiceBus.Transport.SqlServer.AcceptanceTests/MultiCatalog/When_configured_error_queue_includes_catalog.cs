namespace NServiceBus.Transport.SqlServer.AcceptanceTests.MultiCatalog
{
    using System;
    using System.Threading.Tasks;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_configured_error_queue_includes_catalog : MultiCatalogAcceptanceTest
    {
        [Test]
        public async Task Error_should_be_sent_to_table_in_configured_catalog()
        {
            await PurgeAllQueues().ConfigureAwait(false);

            try
            {
                var ctx = await Scenario.Define<Context>()
                    .WithEndpoint<Sender>(b =>
                    {
                        b.DoNotFailOnErrorMessages();
                        b.When((bus, c) => bus.SendLocal(new Message()));
                    })
                    .WithEndpoint<ErrorSpy>()
                    .Done(c => c.FailedMessageProcessed)
                    .Run();

                Assert.True(ctx.FailedMessageProcessed, "Message should be moved to error queue in custom schema");
            }
            finally
            {
                await PurgeAllQueues().ConfigureAwait(false);
            }
        }

        static Task PurgeAllQueues()
        {
            return Task.WhenAll(PurgeQueues(SenderConnectionString), PurgeQueues(SpyConnectionString));
        }

        static async Task PurgeQueues(string connectionString)
        {
            var errorSpyName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy));

            using (var conn = new SqlConnection(connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);
                var command = conn.CreateCommand();
                command.CommandText = $@"IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{errorSpyName}]') AND type in (N'U'))
BEGIN
    DELETE FROM dbo.{errorSpyName}
END";
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public class Context : ScenarioContext
        {
            public bool FailedMessageProcessed { get; set; }
        }

        public class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var errorSpyName = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(ErrorSpy));

                    c.SendFailedMessagesTo($"{errorSpyName}@[dbo]@[nservicebus2]");

                    c.Recoverability()
                        .Immediate(i => i.NumberOfRetries(0))
                        .Delayed(d => d.NumberOfRetries(0));

                    c.UseTransport<SqlServerTransport>()
                        .ConnectionString(SenderConnectionString);
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    throw new Exception("Simulated exception");
                }
            }
        }

        public class ErrorSpy : EndpointConfigurationBuilder
        {
            public ErrorSpy()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<SqlServerTransport>()
                        .ConnectionString(SpyConnectionString);
                });
            }

            class Handler : IHandleMessages<Message>
            {
                public Context Context { get; set; }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    Context.FailedMessageProcessed = true;

                    return Task.FromResult(0);
                }
            }
        }

        public class Message : ICommand { }

        static string SenderConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus1");
        static string SpyConnectionString => WithCustomCatalog(GetDefaultConnectionString(), "nservicebus2");
    }
}