namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using SqlServer;

    public class When_checking_schema
    {
        const string QueueTableName = "CheckingSchema";

        SqlTableBasedQueue queue;
        SqlServerConstants sqlConstants = new();

        [SetUp]
        public async Task SetUp()
        {
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString, ConnectionPoolValidator.Validate);

            await ResetQueue(addressParser, dbConnectionFactory);

            queue = new SqlTableBasedQueue(sqlConstants, addressParser.Parse(QueueTableName).QualifiedTableName, QueueTableName, false);
        }

        [Test]
        public async Task It_returns_type_for_headers_column()
        {
            using (var connection = await dbConnectionFactory.OpenNewConnection())
            {
                var type = await queue.CheckHeadersColumnType(connection);
                Assert.AreEqual("nvarchar", type);
            }
        }

        SqlServerDbConnectionFactory dbConnectionFactory;

        async Task ResetQueue(QueueAddressTranslator addressTranslator, SqlServerDbConnectionFactory dbConnectionFactory, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, dbConnectionFactory, addressTranslator.Parse);

            using (var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                using (var comm = connection.CreateCommand())
                {
                    comm.CommandText = $"IF OBJECT_ID('{QueueTableName}', 'U') IS NOT NULL DROP TABLE {QueueTableName}";
                    comm.ExecuteNonQuery();
                }
            }
            await queueCreator.CreateQueueIfNecessary(new[] { QueueTableName }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken).ConfigureAwait(false);
        }
    }
}