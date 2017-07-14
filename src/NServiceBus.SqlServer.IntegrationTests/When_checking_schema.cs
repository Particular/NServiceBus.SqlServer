namespace NServiceBus.SqlServer.AcceptanceTests.TransportTransaction
{
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Transport.SQLServer;
    using Transport;

    public class When_checking_schema
    {
        const string QueueTableName = "CheckingSchema";

        TableBasedQueue queue;

        [SetUp]
        public async Task SetUp()
        {
            var addressParser = new QueueAddressParser("dbo", null, null);

            await ResetQueue(addressParser);

            queue = new TableBasedQueue(addressParser.Parse(QueueTableName));
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

        static SqlConnectionFactory sqlConnectionFactory = SqlConnectionFactory.Default(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");

        static async Task ResetQueue(QueueAddressParser addressParser)
        {
            var queueCreator = new QueueCreator(sqlConnectionFactory, addressParser);
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