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

        [TestCase("nservicebus")]
        [TestCase("nservicebus1")]
        public async Task It_reports_a_heap_as_a_heap(string catalog)
        {
            var isHeap = await CheckIfNewTableIsHeap(catalog, sqlConstants.CreateQueueText);
            Assert.That(isHeap, Is.True);
        }

        [TestCase("nservicebus")]
        [TestCase("nservicebus1")]
        public async Task It_does_not_report_a_bucketed_table_as_a_heap(string catalog)
        {
            var isHeap = await CheckIfNewTableIsHeap(catalog, sqlConstants.CreateBucketedQueueText);
            Assert.That(isHeap, Is.False);
        }

        SqlServerDbConnectionFactory dbConnectionFactory;

        async Task<bool> CheckIfNewTableIsHeap(string catalog, string creationScript, CancellationToken cancellationToken = default)
        {
            // the connection stays in the nservicebus catalog, whatever catalog the table is in
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            var address = $"CheckingHeap@[dbo]@[{catalog}]";
            await ResetQueue(addressParser, dbConnectionFactory, address, creationScript, cancellationToken);

            var tableQueue = new SqlTableBasedQueue(sqlConstants, addressParser.Parse(address), address, false);
            await using var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken);
            return await tableQueue.CheckIfTableIsHeap(connection, cancellationToken);
        }

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
                var qualifiedTableName = addressTranslator.Parse(tableName).QualifiedTableName;
                comm.CommandText = $"IF OBJECT_ID('{qualifiedTableName}', 'U') IS NOT NULL DROP TABLE {qualifiedTableName}";
                comm.ExecuteNonQuery();
            }
            await queueCreator.CreateQueueIfNecessary(new[] { tableName }, new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"), cancellationToken).ConfigureAwait(false);
        }
    }
}