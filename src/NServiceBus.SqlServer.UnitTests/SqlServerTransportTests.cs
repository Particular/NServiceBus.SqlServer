namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Settings;
    using Transport.SQLServer;

    [TestFixture]
    public class SqlServerTransportTests
    {
        [Test]
        public void It_rejects_connection_string_without_catalog_property()
        {
            var definition = new SqlServerTransport();
            var subscriptionSettings = new SubscriptionSettings();
            subscriptionSettings.DisableSubscriptionCache();
            var settings = new SettingsHolder();
            settings.Set(subscriptionSettings);

            Assert.That( () => definition.Initialize(settings, @"Data Source=.\SQLEXPRESS;Integrated Security=True"),
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

            var subscriptionSettings = new SubscriptionSettings();
            subscriptionSettings.DisableSubscriptionCache();
            var settings = new SettingsHolder();
            settings.Set(subscriptionSettings);

            definition.Initialize(settings, connectionString);
            Assert.Pass();
        }
    }
}