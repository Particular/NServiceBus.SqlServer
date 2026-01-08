namespace NServiceBus.Transport.PostgreSql.AcceptanceTests;

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcceptanceTesting;
using AcceptanceTesting.Customization;
using Logging;
using Npgsql;
using NpgsqlTypes;
using NServiceBus.AcceptanceTests;
using NServiceBus.AcceptanceTests.EndpointTemplates;
using NUnit.Framework;

public class When_a_corrupted_message_is_received : NServiceBusAcceptanceTest
{
    [SetUp]
    public void SetConnectionString() =>
        connectionString = Environment.GetEnvironmentVariable("PostgreSqlTransportConnectionString") ?? @"User ID=user;Password=admin;Host=localhost;Port=54320;Database=nservicebus;Pooling=true;Connection Lifetime=0;Include Error Detail=true";

    [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
    [TestCase(TransportTransactionMode.ReceiveOnly)]
    [TestCase(TransportTransactionMode.None)]
    public async Task Should_move_it_to_the_error_queue_when_headers_corrupted(TransportTransactionMode txMode)
    {
        if (txMode == TransportTransactionMode.TransactionScope && !OperatingSystem.IsWindows())
        {
            Assert.Ignore("Transaction scope mode is only supported on windows");
        }

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

                        using (var conn = new NpgsqlConnection(connectionString))
                        {
                            await conn.OpenAsync();
                            var command = conn.CreateCommand();
                            var guid = Guid.NewGuid();
                            command.CommandText =
                                $@"INSERT INTO public.""{endpoint}""(id, expires, headers, body)
                                    VALUES (@Id,@Expires,@Headers,@Body)";
                            command.Parameters.Add("Id", NpgsqlDbType.Uuid).Value = guid;
                            command.Parameters.Add("Expires", NpgsqlDbType.Timestamp).Value = DBNull.Value;
                            command.Parameters.Add("Headers", NpgsqlDbType.Text).Value = "<corrupted headers";

                            command.Parameters.Add("Body", NpgsqlDbType.Bytea).Value = Encoding.UTF8.GetBytes("");

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

            Assert.That(MessageExistsInErrorQueue(connectionString), Is.True, "The message should have been moved to the error queue");
        }
        finally
        {
            PurgeQueues(connectionString);
        }
    }

    static void PurgeQueues(string connectionString)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            var command = conn.CreateCommand();
            command.CommandText = $@"
                    DO $$
                    BEGIN
                      IF EXISTS(SELECT * FROM public.""{errorQueueName}"")
                      then
                          DELETE FROM public.""{errorQueueName}"";
                      end if;
                    END;
                    $$";
            command.ExecuteNonQuery();
        }
    }

    static bool MessageExistsInErrorQueue(string connectionString)
    {
        using (var conn = new NpgsqlConnection(connectionString))
        {
            conn.Open();
            var command = conn.CreateCommand();
            command.CommandText = $@"SELECT COUNT(1) FROM public.""{errorQueueName}""";
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