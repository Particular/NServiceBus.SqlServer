namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Settings;

    [TestFixture]
    public class SqlServerTransportTests
    {
        [Test]
        public void It_rejects_connection_string_without_catalog_property()
        {
            var definition = new SqlServerTransport();
            Assert.That( () => definition.Initialize(new SettingsHolder(), @"Data Source=.\SQLEXPRESS;Integrated Security=True"),
                Throws.Exception.Message.Contains("Initial Catalog property is mandatory in the connection string."));
        }

        [Test]
        [TestCase("Initial catalog=my.catalog")]
        [TestCase("Initial Catalog=my.catalog")]
        [TestCase("Database=my.catalog")]
        [TestCase("database=my.catalog")]
        public void It_accepts_connection_string_with_catalog_property(string connectionString)
        {
            var definition = new SqlServerTransport();
            definition.Initialize(new SettingsHolder(), connectionString);
            Assert.Pass();
        }
    }
}