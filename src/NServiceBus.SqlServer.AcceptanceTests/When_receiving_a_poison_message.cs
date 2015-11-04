namespace NServiceBus.AcceptanceTests.Basic
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports.SQLServer.Light;
    using NUnit.Framework;

    public class When_receiving_a_poison_message : NServiceBusAcceptanceTest
    {
        const string ConnectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";

        const string InsertPoisonMessageCommand = "INSERT [dbo].[Basic.Receiver.WhenReceivingAPoisonMessage.{1}] ([Id], [CorrelationId], [ReplyToAddress], [Recoverable], [Expires], [Headers], [Body]) VALUES (N'{0}', NULL, N'InvalidType', 1, NULL, N'<InvalidJson/>', 0x0)";
        const string CheckDlqMessageCountCommand = "SELECT COUNT(*) FROM [dbo].[Error] WHERE [Id] = '{0}'";
        const string CheckInputQueueMessageCountCommand = "SELECT COUNT(*) FROM [dbo].[Basic.Receiver.WhenReceivingAPoisonMessage.{1}] WHERE [Id] = '{0}'";

        [Test]
        public void Should_move_the_message_to_error_queue_DTC()
        {
            const string mode = "DTC";
            var ctx = new Context
            {
                Id = Guid.NewGuid()
            };
            Scenario.Define(ctx)
                .WithEndpoint<ReceiverDTC>(x =>  x.Given((b, c) => InsertPoisonMessage(c, mode)))
                .Done(CheckErrorQueue)
                .AllowExceptions()
                .Run(TimeSpan.FromSeconds(20));

            AssertNoMessagesInInputQueue(ctx, mode);
        }
        
        [Test]
        public void Should_move_the_message_to_error_queue_local()
        {
            const string mode = "Local";
            var ctx = new Context
            {
                Id = Guid.NewGuid()
            };
            Scenario.Define(ctx)
                .WithEndpoint<ReceiverLocal>(x =>  x.Given((b, c) => InsertPoisonMessage(c, mode)))
                .Done(CheckErrorQueue)
                .AllowExceptions()
                .Run(TimeSpan.FromSeconds(20));

            AssertNoMessagesInInputQueue(ctx, mode);
        }
        
        [Test]
        public void Should_move_the_message_to_error_queue_none()
        {
            const string mode = "None";
            var ctx = new Context
            {
                Id = Guid.NewGuid()
            };
            Scenario.Define(ctx)
                .WithEndpoint<ReceiverNone>(x =>  x.Given((b, c) => InsertPoisonMessage(c, mode)))
                .Done(CheckErrorQueue)
                .AllowExceptions()
                .Run(TimeSpan.FromSeconds(20));

            AssertNoMessagesInInputQueue(ctx, mode);
        }
        

        static void AssertNoMessagesInInputQueue(Context c, string mode)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(string.Format(CheckInputQueueMessageCountCommand, c.Id, mode), conn)
                {
                    CommandType = CommandType.Text
                })
                {
                    var count = (int)cmd.ExecuteScalar();
                    Assert.AreEqual(0, count);
                }
            }
        }

        static bool CheckErrorQueue(Context c)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(string.Format(CheckDlqMessageCountCommand, c.Id), conn)
                {
                    CommandType = CommandType.Text
                })
                {
                    var count = (int)cmd.ExecuteScalar();
                    return count == 1;
                }
            }
        }

        static void InsertPoisonMessage(Context c, string mode)
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(string.Format(InsertPoisonMessageCommand, c.Id, mode), conn)
                {
                    CommandType = CommandType.Text
                })
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public class Context : ScenarioContext
        {
            public Guid Id { get; set; }
        }

        public class ReceiverDTC : EndpointConfigurationBuilder
        {
            public ReceiverDTC()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.EndpointName("Basic.Receiver.WhenReceivingAPoisonMessage.DTC");
                    cfg.UseTransport<SqlServerTransport>();
                });
            }
        }
        
        public class ReceiverLocal : EndpointConfigurationBuilder
        {
            public ReceiverLocal()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.EndpointName("Basic.Receiver.WhenReceivingAPoisonMessage.Local");
                    cfg.UseTransport<SqlServerTransport>();
                });
            }
        }
        
        public class ReceiverNone : EndpointConfigurationBuilder
        {
            public ReceiverNone()
            {
                EndpointSetup<DefaultServer>(cfg =>
                {
                    cfg.EndpointName("Basic.Receiver.WhenReceivingAPoisonMessage.None");
                    cfg.UseTransport<SqlServerTransport>();
                });
            }
        }
    }
}
