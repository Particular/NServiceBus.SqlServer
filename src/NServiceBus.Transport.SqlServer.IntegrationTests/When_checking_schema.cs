namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport.SqlServer;
    using Transport;

    public class When_checking_schema
    {
        const string QueueTableName = "CheckingSchema";

        TableBasedQueue queue;

        [SetUp]
        public async Task SetUp()
        {
            var addressParser = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            var connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
            if (string.IsNullOrEmpty(connectionString))
            {
                connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
            }

            sqlConnectionFactory = SqlConnectionFactory.Default(connectionString);

            await ResetQueue(addressParser, sqlConnectionFactory);

            queue = new TableBasedQueue(addressParser.Parse(QueueTableName).QualifiedTableName, QueueTableName);
        }

        [Test]
        public async Task It_returns_type_for_headers_column()
        {
            using (var connection = await sqlConnectionFactory.OpenNewConnection())
            {
                var type = await queue.CheckHeadersColumnType(connection);
                Assert.AreEqual("nvarchar", type);
            }
        }

        SqlConnectionFactory sqlConnectionFactory;

        static async Task ResetQueue(QueueAddressTranslator addressTranslator, SqlConnectionFactory sqlConnectionFactory)
        {
            var queueCreator = new QueueCreator(sqlConnectionFactory, addressTranslator,
                new CanonicalQueueAddress("Delayed", "dbo", "nservicebus"));
            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(QueueTableName);

            using (var connection = await sqlConnectionFactory.OpenNewConnection().ConfigureAwait(false))
            {
                using (var comm = connection.CreateCommand())
                {
                    comm.CommandText = $"IF OBJECT_ID('{QueueTableName}', 'U') IS NOT NULL DROP TABLE {QueueTableName}";
                    comm.ExecuteNonQuery();
                }
            }
            await queueCreator.CreateQueueIfNecessary(queueBindings, "").ConfigureAwait(false);
        }
    }
}