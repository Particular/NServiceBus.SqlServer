namespace NServiceBus.Transport.SqlServer.AcceptanceTests.NativeTimeouts
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTesting.Customization;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;


    class When_configured_to_purge_expired_messages_at_startup : NServiceBusAcceptanceTest
    {
        SqlServerConstants sqlConstants = new();

        [SetUp]
        public void SetUpConnectionString() =>
    connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

        [Test]
        public async Task Should_only_purge_expired_messages()
        {
            await SetupInputQueue();

            var context = await Scenario.Define<Context>()
               .WithEndpoint<TestEndpoint>(b =>
               {
                   b.CustomConfig(c =>
                   {
                       c.ConfigureSqlServerTransport().ExpiredMessagesPurger.PurgeOnStartup = true;
                   });
               })
               .Done(c => QueueIsEmpty())
               .Run();

            Assert.That(context.MessageWasHandled, Is.True, "Non expired message should have been handled.");
        }

        // NOTE: The input queue must exist so that we can send messages to it
        async Task SetupInputQueue()
        {
            var connectionFactory = new SqlServerDbConnectionFactory(async token =>
            {
                var connection = new SqlConnection(connectionString);

                await connection.OpenAsync(token);

                return connection;
            });

            var parser = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (!parser.TryGetValue("Initial Catalog", out var catalogSetting) && !parser.TryGetValue("database", out catalogSetting))
            {
                throw new Exception("Database is not configured on connection string");
            }

            var addressTranslator = new QueueAddressTranslator((string)catalogSetting, "dbo", null, null);
            var queueCreator = new QueueCreator(sqlConstants, connectionFactory, addressTranslator.Parse);

            var endpoint = Conventions.EndpointNamingConvention(typeof(TestEndpoint));
            await queueCreator.CreateQueueIfNecessary(new[] { endpoint }, null);

            var tableBasedQueueCache = new TableBasedQueueCache(
                (address, isStreamSupported) =>
                {
                    var canonicalAddress = addressTranslator.Parse(address);
                    return new SqlTableBasedQueue(sqlConstants, canonicalAddress, canonicalAddress.Address, isStreamSupported);
                },
                s => addressTranslator.Parse(s).Address,
                true);

            var tableBasedQueue = tableBasedQueueCache.Get(endpoint);

            using (var connection = await connectionFactory.OpenNewConnection())
            using (var transaction = connection.BeginTransaction())
            {
                await SendMessage(new Message(), TimeSpan.FromSeconds(5));
                transaction.Commit();

                Task SendMessage<T>(T message, TimeSpan ttbr)
                {
                    var messageId = Guid.NewGuid().ToString();

                    var messageBody = System.Text.Json.JsonSerializer.Serialize(message);
                    var messageBytes = System.Text.Encoding.UTF8.GetBytes(messageBody);

                    var outgoingMessage = new OutgoingMessage(messageId, new Dictionary<string, string>
                    {
                        [Headers.MessageId] = messageId,
                        [Headers.EnclosedMessageTypes] = typeof(T).ToString(),
                        [Headers.ContentType] = ContentTypes.Json
                    }, messageBytes);

                    return tableBasedQueue.Send(outgoingMessage, ttbr, connection, transaction);
                }
            }
        }

        bool QueueIsEmpty()
        {
            var endpoint = Conventions.EndpointNamingConvention(typeof(TestEndpoint));
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand($"SELECT COUNT(*) FROM [dbo].[{endpoint}]", connection))
                {
                    var numberOfMessagesInQueue = (int)command.ExecuteScalar();
                    return numberOfMessagesInQueue == 0;
                }
            }
        }

        class TestEndpoint : EndpointConfigurationBuilder
        {
            public TestEndpoint()
            {
                EndpointSetup<DefaultServer>(config => config.UseSerialization<SystemJsonSerializer>());
            }

            class MessageHandler : IHandleMessages<Message>
            {
                readonly Context testContext;

                public MessageHandler(Context testContext)
                {
                    this.testContext = testContext;
                }

                public Task Handle(Message message, IMessageHandlerContext context)
                {
                    testContext.MessageWasHandled = true;
                    return Task.CompletedTask;
                }
            }
        }

        public class Message : IMessage
        {
        }

        class Context : ScenarioContext
        {
            public bool MessageWasHandled { get; set; }
        }

        string connectionString;
    }
}
