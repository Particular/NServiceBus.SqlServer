namespace NServiceBus.Transport.SqlServer.UnitTests
{
    using NUnit.Framework;
    using SqlServer;

    [TestFixture]
    public class QueueAddressParserTests
    {
        [Test]
        public void Should_not_change_canonical_address()
        {
            var parser = new QueueAddressTranslator(null, "dbo", null, new QueueSchemaAndCatalogOptions());

            var address = "my.tb[e@[my.[sch@ma]@[my.c@ta]]og]";
            var canonicalAddress = parser.Parse(address).Address;

            Assert.AreEqual(address, canonicalAddress);
        }
    }
}