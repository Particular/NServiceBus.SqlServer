namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Data.SqlClient;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.Config;
    using NUnit.Framework;

    [TestFixture]
    class SqlConnectionFactoryConfigTests : ConfigTest
    {
        [Test]
        public void Sql_connection_factory_can_be_customized()
        {
            Func<string, SqlConnection> testFactory = connectionString => new SqlConnection(@"Data Source=.\SQLEXPRESS");

            var busConfig = new BusConfiguration();
            busConfig
                .UseTransport<SqlServerTransport>()
                .UseCustomSqlConnectionFactory(testFactory);

            var builder = Activate(busConfig, new SqlConnectionFactoryConfig());
            var factory = builder.Build<ConnectionFactory>();

            Assert.IsNotNull(factory);
            Assert.AreEqual(@"Data Source=.\SQLEXPRESS", factory.OpenNewConnection("Invalid").ConnectionString);
        }
    }
}
