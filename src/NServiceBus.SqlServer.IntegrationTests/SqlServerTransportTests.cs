namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Settings;
    using Transport.SQLServer;

    [TestFixture]
    public class SqlServerTransportTests
    {
        [Test]
        public void It_reads_catalog_from_open_connection()
        {
            var definition = new SqlServerTransport();
            Func<Task<SqlConnection>> factory = async () =>
            {
                var connection = new SqlConnection(@"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True");
                await connection.OpenAsync().ConfigureAwait(false);
                return connection;
            };
            var settings = new SettingsHolder();
            settings.Set(SettingsKeys.ConnectionFactoryOverride, factory);
            definition.Initialize(settings, "Invalid-connection-string");
            Assert.Pass();
        }
    }
}