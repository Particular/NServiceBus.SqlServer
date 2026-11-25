namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using SqlServer;
    using Transport;

    public class When_creating_queue_tables
    {
        [Test]
        public async Task Default_script_creates_a_heap()
        {
            await CreateQueue(heapQueue, sqlConstants.CreateQueueText);

            var indexIds = await Query(heapQueue, "SELECT CONCAT(index_id, '') FROM sys.indexes WHERE object_id = OBJECT_ID(@table)");

            Assert.That(indexIds, Does.Contain("0"));
        }

        [Test]
        public async Task Bucketed_script_creates_a_table_clustered_on_bucket_and_row_version()
        {
            await CreateQueue(bucketedQueue, sqlConstants.CreateBucketedQueueText);

            Assert.That(await IndexColumns(bucketedQueue), Is.EqualTo(BucketedIndexColumns).AsCollection);
        }

        [Test]
        public async Task Both_scripts_create_the_same_columns_and_nonclustered_indexes()
        {
            await CreateQueue(heapQueue, sqlConstants.CreateQueueText);
            await CreateQueue(bucketedQueue, sqlConstants.CreateBucketedQueueText);

            var heapColumns = await Columns(heapQueue);
            var bucketedColumns = await Columns(bucketedQueue);
            var heapIndexColumns = await IndexColumns(heapQueue);
            var bucketedIndexColumns = await IndexColumns(bucketedQueue);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(bucketedColumns.Where(column => !column.StartsWith("Bucket ")), Is.EqualTo(heapColumns).AsCollection);
                Assert.That(bucketedIndexColumns.Where(column => !column.StartsWith("Index_Bucket_RowVersion ")), Is.EqualTo(heapIndexColumns).AsCollection);
            }
        }

        [Test]
        public async Task Concurrent_creation_creates_one_bucketed_table()
        {
            var creations = Enumerable.Range(0, 20)
                .Select(_ => Task.Run(() => CreateQueue(bucketedQueue, sqlConstants.CreateBucketedQueueText)))
                .ToArray();

            await Task.WhenAll(creations);

            var tables = await Query(bucketedQueue, "SELECT name FROM sys.tables WHERE name = PARSENAME(@table, 1)");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(tables, Has.Count.EqualTo(1));
                Assert.That(await IndexColumns(bucketedQueue), Is.EqualTo(BucketedIndexColumns).AsCollection);
            }
        }

        [Test]
        public async Task Bucketed_table_gets_the_body_string_column()
        {
            await CreateQueue(bucketedQueue, sqlConstants.CreateBucketedQueueText, createMessageBodyColumn: true);

            // BodyString casts the stored bytes to nvarchar, so the body has to be UTF-16
            const string body = "A message body";
            var queue = new SqlTableBasedQueue(sqlConstants, bucketedQueue, bucketedQueue.Address, true);
            await using (var connection = await dbConnectionFactory.OpenNewConnection())
            {
                await queue.Send(new OutgoingMessage("1", [], Encoding.Unicode.GetBytes(body)), TimeSpan.MaxValue, connection, null);
            }

            var bodyStrings = await Query(bucketedQueue, $"SELECT TOP(1) BodyString FROM {bucketedQueue.QualifiedTableName}");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(bodyStrings, Is.EqualTo(new[] { body }).AsCollection);
                Assert.That(await IndexColumns(bucketedQueue), Is.EqualTo(BucketedIndexColumns).AsCollection);
            }
        }

        [SetUp]
        public async Task SetUp()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            heapQueue = addressTranslator.Parse("CreatingHeapQueue");
            bucketedQueue = addressTranslator.Parse("CreatingBucketedQueue");

            await using var connection = await dbConnectionFactory.OpenNewConnection();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS {heapQueue.QualifiedTableName}, {bucketedQueue.QualifiedTableName}";
            await command.ExecuteNonQueryAsync();
        }

        Task CreateQueue(CanonicalQueueAddress queue, string creationScript, bool createMessageBodyColumn = false, CancellationToken cancellationToken = default)
        {
            var queueCreator = new QueueCreator(sqlConstants, creationScript, dbConnectionFactory, addressTranslator.Parse, createMessageBodyColumn);

            return queueCreator.CreateQueueIfNecessary([queue.Address], null, cancellationToken);
        }

        Task<List<string>> Columns(CanonicalQueueAddress queue, CancellationToken cancellationToken = default) => Query(queue, @"
SELECT CONCAT(name, ' type=', TYPE_NAME(user_type_id), ' length=', max_length, ' nullable=', is_nullable, ' identity=', is_identity, ' computed=', is_computed, ' collation=', collation_name)
FROM sys.columns
WHERE object_id = OBJECT_ID(@table)
ORDER BY column_id", cancellationToken);

        Task<List<string>> IndexColumns(CanonicalQueueAddress queue, CancellationToken cancellationToken = default) => Query(queue, @"
SELECT CONCAT(i.name, ' ', i.type_desc COLLATE DATABASE_DEFAULT, ' unique=', i.is_unique, ' key=', ic.key_ordinal, ' descending=', ic.is_descending_key, ' included=', ic.is_included_column, ' column=', c.name, ' filter=', i.filter_definition)
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.object_id = OBJECT_ID(@table)
ORDER BY i.name, ic.key_ordinal, c.name", cancellationToken);

        async Task<List<string>> Query(CanonicalQueueAddress queue, string sql, CancellationToken cancellationToken = default)
        {
            await using var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.AddParameter("@table", DbType.String, queue.QualifiedTableName);

            var rows = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(reader.GetString(0));
            }

            return rows;
        }

        static readonly string[] BucketedIndexColumns =
        [
            "Index_Bucket_RowVersion CLUSTERED unique=1 key=1 descending=0 included=0 column=Bucket filter=",
            "Index_Bucket_RowVersion CLUSTERED unique=1 key=2 descending=0 included=0 column=RowVersion filter=",
            "Index_Expires NONCLUSTERED unique=0 key=0 descending=0 included=1 column=Id filter=([Expires] IS NOT NULL)",
            "Index_Expires NONCLUSTERED unique=0 key=0 descending=0 included=1 column=RowVersion filter=([Expires] IS NOT NULL)",
            "Index_Expires NONCLUSTERED unique=0 key=1 descending=0 included=0 column=Expires filter=([Expires] IS NOT NULL)",
            "Index_RowVersion NONCLUSTERED unique=0 key=1 descending=0 included=0 column=RowVersion filter="
        ];

        readonly SqlServerConstants sqlConstants = new();
        SqlServerDbConnectionFactory dbConnectionFactory;
        QueueAddressTranslator addressTranslator;
        CanonicalQueueAddress heapQueue;
        CanonicalQueueAddress bucketedQueue;
    }
}
