namespace NServiceBus.SqlServer.UnitTests
{
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class QueueAddressTests
    {

        [Test]
        [TestCase("table", "schema", "table@[schema]")]
        [TestCase("my.table", "my.schema", "my.table@[my.schema]")]
        [TestCase("table", "", "table")]
        [TestCase("table", null, "table")]
        [TestCase("table", "[my.schema]", "table@[my.schema]")]
        [TestCase("[my.table]", "[my.schema]", "[my.table]@[my.schema]")]
        public void Should_include_brackets_around_schema_name(string tableName, string schemaName, string expectedAddress)
        {
            var address = new QueueAddress(tableName, schemaName);

            Assert.AreEqual(expectedAddress, address.ToString());
        }


        [Test]
        [TestCase("table@schema", "table", "schema")]
        [TestCase("my.table@my.schema@my.schema", "my.table", "my.schema")]
        [TestCase("table", "table", null)]
        [TestCase("table@[my.schema]", "table", "my.schema")]
        [TestCase("[my.table]@[my.schema]", "[my.table]", "my.schema")]
        public void Should_parse_address(string transportAddress, string expectedTableName, string expectedSchema)
        {
            var parsedAddress = QueueAddress.Parse(transportAddress);

            Assert.AreEqual(expectedTableName, parsedAddress.TableName);
            Assert.AreEqual(expectedSchema, parsedAddress.SchemaName);
        }
    }
}