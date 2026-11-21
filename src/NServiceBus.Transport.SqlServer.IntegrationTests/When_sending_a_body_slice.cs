namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using SqlServer;
    using Transport;

    public class When_sending_a_body_slice
    {
        [TestCase(0, true)]
        [TestCase(100, true)]
        [TestCase(8000, true)]
        [TestCase(8001, true)]
        [TestCase(200_000, true)]
        [TestCase(100, false)]
        [TestCase(200_000, false)]
        public async Task Should_store_only_the_slice(int bodyLength, bool isStreamSupported)
        {
            var queue = new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, isStreamSupported);
            var buffer = new byte[bodyLength + 64];
            Random.Shared.NextBytes(buffer);
            var body = buffer.AsMemory(13, bodyLength);

            await using var connection = await dbConnectionFactory.OpenNewConnection();
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                await queue.Send(new OutgoingMessage("1", [], body), TimeSpan.MaxValue, connection, transaction);
                await transaction.CommitAsync();
            }

            var result = await queue.TryReceive(connection, null);

            Assert.That(result.Message.Body.ToArray(), Is.EqualTo(body.ToArray()).AsCollection);
        }

        [SetUp]
        public async Task SetUp()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, new QueueSchemaAndCatalogOptions());
            queueAddress = addressTranslator.Parse(QueueName);

            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);
            await queueCreator.CreateQueueIfNecessary([QueueName], new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), CancellationToken.None);

            await new QueuePurger(dbConnectionFactory).Purge(new SqlTableBasedQueue(sqlConstants, queueAddress, queueAddress.Address, true));
        }

        readonly SqlServerConstants sqlConstants = new();
        SqlServerDbConnectionFactory dbConnectionFactory;
        CanonicalQueueAddress queueAddress;

        const string QueueName = "BodySliceTests";
    }
}
