namespace NServiceBus.Transport.SqlServer.IntegrationTests
{
    using System;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using NServiceBus.Transport.Sql.Shared;
    using NUnit.Framework;
    using SqlServer;

    public class When_writing_with_non_default_session_options
    {
        // Each value is the one that indexes on computed or filtered columns reject
        [TestCase("ANSI_NULLS", "OFF")]
        [TestCase("ANSI_PADDING", "OFF")]
        [TestCase("ANSI_WARNINGS", "OFF")]
        [TestCase("ARITHABORT", "OFF")]
        [TestCase("CONCAT_NULL_YIELDS_NULL", "OFF")]
        [TestCase("QUOTED_IDENTIFIER", "OFF")]
        [TestCase("NUMERIC_ROUNDABORT", "ON")]
        public async Task Bucketed_table_has_the_same_outcomes_as_a_heap(string option, string value)
        {
            var heapOutcomes = await InsertUpdateAndDelete(heapQueue, option, value);
            var bucketedOutcomes = await InsertUpdateAndDelete(bucketedQueue, option, value);

            TestContext.Out.WriteLine($"SET {option} {value}: heap [{string.Join(", ", heapOutcomes)}], bucketed [{string.Join(", ", bucketedOutcomes)}]");

            using (Assert.EnterMultipleScope())
            {
                Assert.That(bucketedOutcomes, Is.EqualTo(heapOutcomes).AsCollection);
                Assert.That(heapOutcomes, Is.All.AnyOf("1 row", "error 1934"));
            }
        }

        [SetUp]
        public async Task SetUp()
        {
            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString") ?? @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;TrustServerCertificate=true";
            dbConnectionFactory = new SqlServerDbConnectionFactory(connectionString);

            var addressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);
            heapQueue = addressTranslator.Parse("SessionOptionsHeapQueue");
            bucketedQueue = addressTranslator.Parse("SessionOptionsBucketedQueue");

            await using (var connection = await dbConnectionFactory.OpenNewConnection())
            {
                await Execute(connection, $"DROP TABLE IF EXISTS {heapQueue.QualifiedTableName}, {bucketedQueue.QualifiedTableName}");
            }

            await new QueueCreator(sqlConstants, sqlConstants.CreateQueueText, dbConnectionFactory, addressTranslator.Parse)
                .CreateQueueIfNecessary([heapQueue.Address], null);
            await new QueueCreator(sqlConstants, sqlConstants.CreateBucketedQueueText, dbConnectionFactory, addressTranslator.Parse)
                .CreateQueueIfNecessary([bucketedQueue.Address], null);
        }

        async Task<string[]> InsertUpdateAndDelete(CanonicalQueueAddress queue, string option, string value, CancellationToken cancellationToken = default)
        {
            var table = queue.QualifiedTableName;
            var existingId = Guid.NewGuid();

            await using var connection = await dbConnectionFactory.OpenNewConnection(cancellationToken);
            await Execute(connection, Insert(table, existingId), cancellationToken);

            // A command without parameters runs as a plain batch, so the option stays set on the session
            await Execute(connection, $"SET {option} {value}", cancellationToken);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT CAST(SESSIONPROPERTY('{option}') AS int)";
                Assert.That(await command.ExecuteScalarAsync(cancellationToken), Is.EqualTo(value == "ON" ? 1 : 0), $"{option} is not in effect");
            }

            return
            [
                await Outcome(connection, Insert(table, Guid.NewGuid()), cancellationToken),
                await Outcome(connection, $"UPDATE {table} SET Expires = DATEADD(d, 1, GETUTCDATE()) WHERE Id = '{existingId}'", cancellationToken),
                await Outcome(connection, $"DELETE FROM {table} WHERE Id = '{existingId}'", cancellationToken)
            ];
        }

        static string Insert(string table, Guid id) =>
            $"INSERT INTO {table} (Id, Recoverable, Expires, Headers, Body) VALUES ('{id}', 1, GETUTCDATE(), '', 0x00)";

        static async Task<string> Outcome(DbConnection connection, string statement, CancellationToken cancellationToken)
        {
            try
            {
                return $"{await Execute(connection, statement, cancellationToken)} row";
            }
            catch (SqlException e)
            {
                return $"error {e.Number}";
            }
        }

        static async Task<int> Execute(DbConnection connection, string statement, CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        readonly SqlServerConstants sqlConstants = new();
        SqlServerDbConnectionFactory dbConnectionFactory;
        CanonicalQueueAddress heapQueue;
        CanonicalQueueAddress bucketedQueue;
    }
}
