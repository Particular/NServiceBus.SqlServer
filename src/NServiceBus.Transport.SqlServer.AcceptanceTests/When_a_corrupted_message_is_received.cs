namespace NServiceBus.Transport.SqlServer.AcceptanceTests
{
    using System;
    using System.Data;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Logging;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_a_corrupted_message_is_received : NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetConnectionString()
        {
            connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";
            }
        }

#if NETFRAMEWORK
        [TestCase(TransportTransactionMode.TransactionScope)]
#endif
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.None)]
        public async Task Should_move_it_to_the_error_queue_when_headers_corrupted(TransportTransactionMode txMode)
        {
            PurgeQueues(connectionString);
            try
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>(b =>
                    {
                        b.DoNotFailOnErrorMessages();
                        b.CustomConfig(c =>
                        {
                            c.ConfigureSqlServerTransport().TransportTransactionMode = txMode;
                        });
                        b.When(async (bus, c) =>
                        {
                            var endpoint = Conventions.EndpointNamingConvention(typeof(Endpoint));

                            using (var conn = new SqlConnection(connectionString))
                            {
                                await conn.OpenAsync();
                                var command = conn.CreateCommand();
                                var guid = Guid.NewGuid();
                                command.CommandText =
                                    $@"INSERT INTO [dbo].[{endpoint}] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body])
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)";
                                command.Parameters.Add("Id", SqlDbType.UniqueIdentifier).Value = guid;
                                command.Parameters.Add("CorrelationId", SqlDbType.UniqueIdentifier).Value = guid;
                                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value = "";
                                command.Parameters.Add("Recoverable", SqlDbType.Bit).Value = true;
                                command.Parameters.Add("Expires", SqlDbType.DateTime).Value = DateTime.UtcNow.AddHours(1);
                                command.Parameters.Add("Headers", SqlDbType.VarChar).Value = "<corrupted headers";

                                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = Encoding.UTF8.GetBytes("");

                                await command.ExecuteNonQueryAsync();
                            }
                        });
                    })
                    .Done(c =>
                    {
                        var firstErrorEntry = c.Logs.FirstOrDefault(l => l.Level == LogLevel.Error
                                                                         && l.Message != null
                                                                         && l.Message.StartsWith("Error receiving message. Probable message metadata corruption. Moving to error queue."));
                        c.FirstErrorEntry = firstErrorEntry?.Message;
                        return c.FirstErrorEntry != null;
                    })
                    .Run();

                Assert.True(MessageExistsInErrorQueue(connectionString), "The message should have been moved to the error queue");
            }
            finally
            {
                PurgeQueues(connectionString);
            }
        }

        static void PurgeQueues(string connectionString)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var command = conn.CreateCommand();
                command.CommandText = $@"IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[{errorQueueName}]') AND type in (N'U'))
BEGIN
    DELETE FROM dbo.{errorQueueName}
END";
                command.ExecuteNonQuery();
            }
        }

        static bool MessageExistsInErrorQueue(string connectionString)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var command = conn.CreateCommand();
                command.CommandText = "SELECT COUNT(1) FROM " + errorQueueName;
                var sqlDataReader = command.ExecuteReader();
                sqlDataReader.Read();
                return sqlDataReader.GetInt32(0) == 1;
            }
        }

        string connectionString;
        const string errorQueueName = "error";

        public class Context : ScenarioContext
        {
            public string FirstErrorEntry { get; set; }
        }


        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.SendFailedMessagesTo(errorQueueName);
                });
            }
        }

        [Serializable]
        public class MyMessage : IMessage
        {
        }
    }
}