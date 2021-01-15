using System.Threading.Tasks;

namespace NServiceBus.Transport.SqlServer.UnitTests
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
                (s, exception) => { }, true);
        }
        [Test]
        public void It_rejects_connection_string_without_catalog_property()
        {
            var definition = new SqlServerTransport
            {
                ConnectionString = @"Data Source=.\SQLEXPRESS;Integrated Security=True"
            };
            
            Assert.ThrowsAsync(
                Throws.Exception.Message.Contains("Initial Catalog property is mandatory in the connection string."),
                async () => await definition.Initialize(settings, new ReceiveSettings[0], new string[0]).ConfigureAwait(false));
        }

        [Test]
        [TestCase("Initial catalog=my.catalog")]
        [TestCase("Initial Catalog=my.catalog")]
        [TestCase("Database=my.catalog")]
        [TestCase("database=my.catalog")]
        public async Task It_accepts_connection_string_with_catalog_property(string connectionString)
        {
            var definition = new SqlServerTransport
            {
                ConnectionString = connectionString
            };

            await definition.Initialize(settings, new ReceiveSettings[0], new string[0]).ConfigureAwait(false);

            Assert.Pass();
        }
    }
}