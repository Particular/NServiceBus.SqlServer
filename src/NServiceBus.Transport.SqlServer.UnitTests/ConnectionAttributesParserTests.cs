namespace NServiceBus.Transport.SqlServer.UnitTests
{
    using System;
    using NUnit.Framework;

    [TestFixture]
    public class ConnectionAttributesParserTests
    {
        [Test]
        public void It_rejects_connection_string_without_catalog_property()
        {
            var ex = Assert.Throws<Exception>(() => ConnectionAttributesParser.Parse(@"Data Source=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=true"));

            Assert.That(ex.Message, Does.Contain("Initial Catalog property is mandatory in the connection string."));
        }

        [TestCase("Initial catalog=my.catalog")]
        [TestCase("Initial Catalog=my.catalog")]
        [TestCase("Database=my.catalog")]
        [TestCase("database=my.catalog")]
        public void It_accepts_connection_string_with_catalog_property(string connectionString)
        {
            var attributes = ConnectionAttributesParser.Parse(connectionString);

            Assert.That(attributes.Catalog, Is.EqualTo("my.catalog"));
        }

        [TestCase("Initial Catalog=incorrect.catalog")]
        [TestCase("Database=incorrect.catalog")]
        public void It_overrides_catalog_with_default_catalog(string connectionString)
        {
            var defaultCatalog = "correct.catalog";
            var attributes = ConnectionAttributesParser.Parse(connectionString, defaultCatalog);

            Assert.That(attributes.Catalog, Is.EqualTo(defaultCatalog));
        }
    }
}
