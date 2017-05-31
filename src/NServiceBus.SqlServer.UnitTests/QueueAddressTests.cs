namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Transport.SQLServer;

    [TestFixture]
    public class QueueAddressTests
    {
        [Test]
        [TestCase("my.table", null, null, "my.table")]

        [TestCase("my.table", "my.schema", null, "my.table@[my.schema]")]
        [TestCase("my.table", "[my.schema]", null, "my.table@[my.schema]")]

        [TestCase("my.table", "my.schema", "my.catalog", "my.table@[my.schema]@[my.catalog]")]
        [TestCase("my.table", "my.schema", "[my.catalog]", "my.table@[my.schema]@[my.catalog]")]
        [TestCase("my.table", "[my.schema]", "[my.catalog]", "my.table@[my.schema]@[my.catalog]")]

        [TestCase("my.table", null, "my.catalog", "my.table@[]@[my.catalog]")]
        [TestCase("my.table", null, "[my.catalog]", "my.table@[]@[my.catalog]")]
        public void Should_generate_address(string tableName, string schemaName, string catalogName, string expectedAddress)
        {
            var address = new QueueAddress(tableName, schemaName, catalogName);

            Assert.AreEqual(expectedAddress, address.Value);
        }


        [Test]
        [TestCase("my.table", "my.table", null, null)]

        [TestCase("my.table@[my.schema]", "my.table", "my.schema", null)]
        [TestCase("my.table@[my.sch@ma]", "my.table", "my.sch@ma", null)]
        [TestCase("my.table@[my.sch[[]ma]", "my.table", "my.sch[[]ma", null)]
        [TestCase("my.table@my.schema", "my.table", "my.schema", null)]

        [TestCase("my.table@[my.schema]@[my.catalog]", "my.table", "my.schema", "my.catalog")]
        [TestCase("my.table@[my.schema]@[my.c@talog]", "my.table", "my.schema", "my.c@talog")]
        [TestCase("my.table@[my.schema]@[my.c[[]talog]", "my.table", "my.schema", "my.c[[]talog")]
        [TestCase("my.table@[my.schema]@my.catalog", "my.table", "my.schema", "my.catalog")]
        [TestCase("my.table@my.schema@my.catalog", "my.table", "my.schema", "my.catalog")]

        [TestCase("my.table@[]@[my.catalog]", "my.table", null, "my.catalog")]
        [TestCase("my.table@[]@my.catalog", "my.table", null, "my.catalog")]

        [TestCase("my.table@my]schema", "my.table", "my]schema", null)]
        [TestCase("my.table@my]]schema", "my.table", "my]]schema", null)]
        [TestCase("my.table@[my]]schema]", "my.table", "my]schema", null)]
        [TestCase("my.table@my[schema", "my.table", "my[schema", null)]
        public void Should_parse_address(string transportAddress, string expectedTableName, string expectedSchema, string expectedCatalog)
        {
            var parsedAddress = QueueAddress.Parse(transportAddress);

            Assert.AreEqual(expectedTableName, parsedAddress.Table);
            Assert.AreEqual(expectedSchema, parsedAddress.Schema);
            Assert.AreEqual(expectedCatalog, parsedAddress.Catalog);
        }
    }
}