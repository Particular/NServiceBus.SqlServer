
namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports;
    using NUnit.Framework;

    public class When_a_corrupted_message_is_received : NServiceBusAcceptanceTest
    {
        const string connString = @"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;";
        const string errorQueueName = "error";

        [TestCase(TransactionSupport.Distributed)]
        [TestCase(TransactionSupport.MultiQueue)]
        [TestCase(TransactionSupport.None)]
        public async Task Should_move_it_to_the_error_queue(TransactionSupport txMode)
        {
            PurgeQueues();
            try
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>(b =>
                    {
                        b.DoNotFailOnErrorMessages();
                        b.CustomConfig(c =>
                        {
                            switch (txMode)
                            {
                                case TransactionSupport.Distributed:
                                    c.Transactions().EnableDistributedTransactions();
                                    break;
                                case TransactionSupport.MultiQueue:
                                    c.Transactions().DisableDistributedTransactions();
                                    break;
                                case TransactionSupport.None:
                                    c.Transactions().Disable();
                                    break;
                            }
                        });
                        b.When((bus, c) =>
                        {
                            var endpoint = AcceptanceTesting.Customization.Conventions.EndpointNamingConvention(typeof(Endpoint));

                            using (var conn = new SqlConnection(connString))
                            {
                                conn.Open();
                                var command = conn.CreateCommand();
                                var guid = Guid.NewGuid();
                                command.CommandText =
                                    $@"INSERT INTO [dbo].[{endpoint}] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body]) 
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)";
                                command.Parameters.Add("Id", SqlDbType.UniqueIdentifier).Value = guid;
                                command.Parameters.Add("CorrelationId", SqlDbType.UniqueIdentifier).Value = guid;
                                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value = "";
                                command.Parameters.Add("Recoverable", SqlDbType.Bit).Value = true;
                                command.Parameters.Add("Expires", SqlDbType.DateTime).Value = DateTime.Now.AddHours(1);
                                command.Parameters.Add("Headers", SqlDbType.VarChar).Value = "<invalid header";
                                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = Encoding.UTF8.GetBytes("<crap><bla>0</bla></crap>");

                                command.ExecuteNonQuery();
                            }

                            return Task.FromResult(0);
                        });
                    })
                    .Done(c => c.Logs.Any(l => l.Level == "error"))
                    .Run();

                Assert.True(MessageExistsInErrorQueue(), "The message should have been moved to the error queue");
            }
            finally
            {
                PurgeQueues();
            }
        }

        static void PurgeQueues()
        {
            using (var conn = new SqlConnection(connString))
            {
                conn.Open();
                var command = conn.CreateCommand();
                command.CommandText = @"DELETE FROM " + errorQueueName;
                command.ExecuteNonQuery();  
            }
        }

        static bool MessageExistsInErrorQueue()
        {
            using (var conn = new SqlConnection(connString))
            {
                conn.Open();
                var command = conn.CreateCommand();
                command.CommandText = @"SELECT COUNT(1) FROM " + errorQueueName;
                var sqlDataReader = command.ExecuteReader();
                sqlDataReader.Read();
                return sqlDataReader.GetInt32(0) == 1;
            }
        }

        public class Context : ScenarioContext
        {
        }


        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(c =>
                {   
                    c.UseTransport<SqlServerTransport>();
                    c.SendFailedMessagesTo(errorQueueName);
                });
            }
        }
    }
}

