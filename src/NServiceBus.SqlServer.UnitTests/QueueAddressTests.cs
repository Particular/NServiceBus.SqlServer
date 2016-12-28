namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Transport.SQLServer;

    [TestFixture]
    public class QueueAddressTests
    {
        [Test]
        [TestCase("table", "schema", null, "table@[schema]")]
        [TestCase("my.table", "my.schema", null, "my.table@[my.schema]")]
        [TestCase("table", "", null, "table@[]")]
        [TestCase("table", null, null, "table")]
        [TestCase("table", "[my.schema]", null, "table@[my.schema]")]
        [TestCase("my.table", "[my.schema]", null, "my.table@[my.schema]")]
        [TestCase("my.table", "[my.schema]", "[my.catalog]", "my.table@[my.schema]@[my.catalog]")]
        [TestCase("my.table", "my.schema", "my.catalog", "my.table@[my.schema]@[my.catalog]")]
        [TestCase("my.table", "", "my.catalog", "my.table@[]@[my.catalog]")]
        public void Should_include_brackets_around_schema_name(string tableName, string schemaName, string catalogName, string expectedAddress)
        {
            var address = new QueueAddress(catalogName, schemaName, tableName);

            Assert.AreEqual(expectedAddress, address.ToString());
        }


        [Test]
        [TestCase("table@schema", "table", "schema", null)]
        [TestCase("my.table@my.schema", "my.table", "my.schema", null)]
        [TestCase("table", "table", null, null)]
        [TestCase("table@[my.schema]", "table", "my.schema", null)]
        [TestCase("my.table@[my.schema]", "my.table", "my.schema", null)]
        [TestCase("my.table@[my.schema]@[my.catalog]", "my.table", "my.schema", "my.catalog")]
        [TestCase("my.table@my.schema@my.catalog", "my.table", "my.schema", "my.catalog")]
        [TestCase("my.table@[]@[my.catalog]", "my.table", "", "my.catalog")]
        public void Should_parse_address(string transportAddress, string expectedTableName, string expectedSchema, string expectedCatalog)
        {
            var parsedAddress = QueueAddress.Parse(transportAddress);

            Assert.AreEqual(expectedTableName, parsedAddress.TableName);
            Assert.AreEqual(expectedSchema, parsedAddress.SchemaName);
            Assert.AreEqual(expectedCatalog, parsedAddress.Catalog);
        }
    }
}