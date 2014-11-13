namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;

    public class When_receiving_a_poison_message: NServiceBusAcceptanceTest
    {
        const string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";

        const string InsertPoisonMessageCommand = "INSERT [dbo].[Basic.Receiver.WhenReceivingAPoisonMessage] ([Id], [CorrelationId], [ReplyToAddress], [Recoverable], [Expires], [Headers], [Body]) VALUES (N'{0}', NULL, N'InvalidType', 1, NULL, N'<InvalidJson/>', 0x0)";
        const string CheckDlqMessageCountCommand = "SELECT COUNT(*) FROM [dbo].[Error] WHERE [Id] = '{0}'";
        const string CheckInputQueueMessageCountCommand = "SELECT COUNT(*) FROM [dbo].[Basic.Receiver.WhenReceivingAPoisonMessage] WHERE [Id] = '{0}'";

        [Test]
        public void Should_move_the_message_to_error_queue()
        {
            Scenario.Define(() => new Context { Id = Guid.NewGuid() })
                    
                    .WithEndpoint<Receiver>(x => x.Given((b, c) =>
                    {
                        using (var conn = new SqlConnection(ConnectionString))
                        {
                            conn.Open();
                            using (var cmd = new SqlCommand(string.Format(InsertPoisonMessageCommand, c.Id), conn)
                            {
                                CommandType = CommandType.Text
                            })
                            {
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }))
                    .Done(c =>
                    {
                        using (var conn = new SqlConnection(ConnectionString))
                        {
                            conn.Open();
                            using (var cmd = new SqlCommand(string.Format(CheckDlqMessageCountCommand, c.Id), conn)
                            {
                                CommandType = CommandType.Text
                            })
                            {
                                var count  = (int)cmd.ExecuteScalar();
                                return count == 1;
                            }
                        }
                    })
                    .AllowExceptions()
                    .Repeat(r => r.For<AllTransactionSettings>())
                    .Should(c =>
                    {
                        using (var conn = new SqlConnection(ConnectionString))
                        {
                            conn.Open();
                            using (var cmd = new SqlCommand(string.Format(CheckInputQueueMessageCountCommand, c.Id), conn)
                            {
                                CommandType = CommandType.Text
                            })
                            {
                                var count = (int)cmd.ExecuteScalar();
                                Assert.AreEqual(0, count);
                            }
                        }
                    })
                    .Run(TimeSpan.FromSeconds(20));
        }

        public class Context : ScenarioContext
        {
            public Guid Id { get; set; }
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.EndpointName("Basic.Receiver.WhenReceivingAPoisonMessage");
                    cfg.UseTransport<SqlServerTransport>();                    
                });
            }
        }
    }
}
