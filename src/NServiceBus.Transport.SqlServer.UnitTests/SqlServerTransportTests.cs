﻿namespace NServiceBus.Transport.SqlServer.UnitTests
{
    using NUnit.Framework;

    [TestFixture]
    public class SqlServerTransportTests
    {
        HostSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = new HostSettings(string.Empty, string.Empty, new StartupDiagnosticEntries(),
                (_, __, ___) => { }, true);
        }
        [Test]
        public void It_rejects_connection_string_without_catalog_property()
        {
            var definition = new SqlServerTransport(@"Data Source=.\SQLEXPRESS;Integrated Security=True");

            Assert.That(
                async () => await definition.Initialize(settings, new ReceiveSettings[0], new string[0], default).ConfigureAwait(false),
                Throws.Exception.Message.Contains("Initial Catalog property is mandatory in the connection string."));
        }

        [Test]
        [TestCase("Initial catalog=my.catalog")]
        [TestCase("Initial Catalog=my.catalog")]
        [TestCase("Database=my.catalog")]
        [TestCase("database=my.catalog")]
        public void It_accepts_connection_string_with_catalog_property(string connectionString)
        {
            new SqlServerTransport(connectionString).GetDefaultCatalog();

            Assert.Pass();
        }
    }
}