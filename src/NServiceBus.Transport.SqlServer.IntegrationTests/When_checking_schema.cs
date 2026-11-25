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
        const string BucketedQueueTableName = "CheckingBucketedSchema";

        SqlTableBasedQueue queue;
        SqlServerConstants sqlConstants = new();

        [SetUp]
        public async Task SetUp()
        {
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";

            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            await ResetQueue(addressParser, dbConnectionFactory, QueueTableName, sqlConstants.CreateQueueText);

            queue = new SqlTableBasedQueue(sqlConstants, addressParser.Parse(QueueTableName), QueueTableName, false);
        }

        [Test]
        public async Task It_returns_type_for_headers_column()
        {
            await using var connection = await dbConnectionFactory.OpenNewConnection();
            var type = await queue.CheckHeadersColumnType(connection);
            Assert.That(type, Is.EqualTo("nvarchar"));
        }

        [Test]
        public async Task It_finds_one_row_version_index_on_a_bucketed_table()
        {
            var count = await RunCheckOnBucketedTable(sqlConstants.CheckIfNonClusteredRowVersionIndexIsPresent);
            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public async Task It_finds_one_expires_index_on_a_bucketed_table()
        {
            var count = await RunCheckOnBucketedTable(sqlConstants.CheckIfExpiresIndexIsPresent);
            Assert.That(count, Is.EqualTo(1));
        }

        SqlServerDbConnectionFactory dbConnectionFactory;

        async Task<int> RunCheckOnBucketedTable(string checkText, CancellationToken cancellationToken = default)
        {
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            await ResetQueue(addressParser, dbConnectionFactory, BucketedQueueTableName, sqlConstants.CreateBucketedQueueText, cancellationToken);

            await using var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = string.Format(checkText, addressParser.Parse(BucketedQueueTableName).QualifiedTableName);
            return (int)await command.ExecuteScalarAsync(cancellationToken);
        }

        async Task ResetQueue(QueueAddressTranslator addressTranslator, SqlServerDbConnectionFactory dbConnectionFactory, string tableName, string creationScript, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, creationScript, dbConnectionFactory, addressTranslator.Parse);

            await using (var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken).ConfigureAwait(false))
            {
                await using var comm = connection.CreateCommand();
                comm.CommandText = $"IF OBJECT_ID('{tableName}', 'U') IS NOT NULL DROP TABLE {tableName}";
                comm.ExecuteNonQuery();
            }
            await queueCreator.CreateQueueIfNecessary(new[] { tableName }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken).ConfigureAwait(false);
        }
    }
}