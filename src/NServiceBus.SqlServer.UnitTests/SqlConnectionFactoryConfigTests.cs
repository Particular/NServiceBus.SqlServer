namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Data.SqlClient;
    using System.Reflection;
    using NServiceBus.Transports.SQLServer.Config;
    using NUnit.Framework;

    [TestFixture]
    class SqlConnectionFactoryConfigTests : ConfigTest
    {
        [Test]
        public void sql_connection_factory_exists_by_default()
        {
            var defaultFactoryMethod = typeof(SqlConnectionFactoryConfig).GetMethod("DefaultOpenNewConnection", BindingFlags.Static | BindingFlags.NonPublic);

            var busConfig = new BusConfiguration();
            busConfig.UseTransport<SqlServerTransport>();

            var builder = Activate(busConfig, new SqlConnectionFactoryConfig());
            var factory = builder.Build<Func<string, SqlConnection>>();

            Assert.IsNotNull(factory);
            Assert.AreEqual(defaultFactoryMethod, factory.Method);
        }

        [Test]
        public void sql_connection_factory_can_be_customized()
        {
            Func<string, SqlConnection> testFactory = connectionString => { throw new Exception("Error"); };

            var busConfig = new BusConfiguration();
            busConfig
                .UseTransport<SqlServerTransport>()
                .UseCustomSqlConnectionFactory(testFactory);

            var builder = Activate(busConfig, new SqlConnectionFactoryConfig());
            var factory = builder.Build<Func<string, SqlConnection>>();

            Assert.IsNotNull(factory);
            Assert.AreEqual(factory, testFactory);
        }
    }
}
