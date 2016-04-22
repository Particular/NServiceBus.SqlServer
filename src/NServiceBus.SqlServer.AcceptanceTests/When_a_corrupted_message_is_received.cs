namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_a_corrupted_message_is_received : NServiceBusAcceptanceTest
    {
        [TestCase(TransportTransactionMode.TransactionScope)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.None)]
        public async Task Should_move_it_to_the_error_queue_when_headers_corrupted(TransportTransactionMode txMode)
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
                            c.UseTransport<SqlServerTransport>()
                                .Transactions(txMode);
                        });
                        b.When((bus, c) =>
                        {
                            var endpoint = Conventions.EndpointNamingConvention(typeof(Endpoint));

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
                                command.Parameters.Add("Headers", SqlDbType.VarChar).Value = "<corrupted headers";

                                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = Encoding.UTF8.GetBytes("");

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

        [TestCase(TransportTransactionMode.TransactionScope)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.None)]
        public async Task Should_move_it_to_the_error_queue_when_body_corrupted(TransportTransactionMode txMode)
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
                            c.UseTransport<SqlServerTransport>()
                                .Transactions(txMode);
                        });
                        b.When((bus, c) =>
                        {
                            var endpoint = Conventions.EndpointNamingConvention(typeof(Endpoint));

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
                                command.Parameters.Add("Headers", SqlDbType.VarChar).Value = "{\"NServiceBus.MessageIntent\":\"Send\",\"NServiceBus.CorrelationId\":\"{guid}\"," +
                                                                                             "\"NServiceBus.OriginatingMachine\":\"SCHMETTERLING\",\"NServiceBus.OriginatingEndpoint\":\"ReceivingWithNativeMultiQueueTransaction.Endpoint\"," +
                                                                                             "\"$.diagnostics.originating.hostid\":\"3c921610dc4d3ddde4347cd65addcb9f\"," +
                                                                                             "\"NServiceBus.ReplyToAddress\":\"ReceivingWithNativeMultiQueueTransaction.Endpoint@dbo\"," +
                                                                                             "\"NServiceBus.ContentType\":\"text/xml\"," +
                                                                                             "\"NServiceBus.EnclosedMessageTypes\":\"NServiceBus.AcceptanceTests.Tx.When_receiving_with_native_multi_queue_transaction + When_receiving_with_native_multi_queue_transaction.MessageHandledEvent, NServiceBus.SqlServer.AcceptanceTests, Version = 0.0.0.0, Culture = neutral, PublicKeyToken = null\"" +
                                                                                             ",\"NServiceBus.RelatedTo\":\"{guid}\"," +
                                                                                             "\"NServiceBus.ConversationId\":\"{guid}\"," +
                                                                                             "\"NServiceBus.MessageId\":\"{guid}\"," +
                                                                                             "\"NServiceBus.Version\":\"6.0.0\"," +
                                                                                             "\"NServiceBus.TimeSent\":\"2015-12-16 11:53:22:631646 Z\"}";

                                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = Encoding.UTF8.GetBytes("body corrupted");

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
                command.CommandText = "DELETE FROM " + errorQueueName;
                command.ExecuteNonQuery();
            }
        }

        static bool MessageExistsInErrorQueue()
        {
            using (var conn = new SqlConnection(connString))
            {
                conn.Open();
                var command = conn.CreateCommand();
                command.CommandText = "SELECT COUNT(1) FROM " + errorQueueName;
                var sqlDataReader = command.ExecuteReader();
                sqlDataReader.Read();
                return sqlDataReader.GetInt32(0) == 1;
            }
        }

        const string connString = @"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;";
        const string errorQueueName = "error";

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

        [Serializable]
        public class MyMessage : IMessage
        {
        }
    }
}