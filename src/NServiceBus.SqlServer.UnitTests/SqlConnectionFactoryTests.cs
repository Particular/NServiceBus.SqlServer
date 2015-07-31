using NUnit.Framework;

namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.Config;

    [TestFixture]
    public class SqlConnectionFactoryTests
    {
        [Test]
        public void Empty_Class_Name_Leads_To_Null_Factory()
        {
            var factoryInstance = SqlConnectionFactory.ConnectionFactoryInstance("");
            Assert.IsNull(factoryInstance);
        }

        [Test]
        public void Null_Class_Name_Leads_To_Null_Factory()
        {
            var factoryInstance = SqlConnectionFactory.ConnectionFactoryInstance(null);
            Assert.IsNull(factoryInstance);
        }

        [Test]
        public void Non_Existing_Class_Name_Leads_To_Exception()
        {
            Assert.Throws<TypeLoadException>(() => SqlConnectionFactory.ConnectionFactoryInstance("NServiceBus.SqlServer.UnitTests.ClassThatDoesNotExists, NServiceBus.SqlServer.UnitTests"));
        }

        [Test]
        public void Class_That_Does_Not_Implement_ISqlConnectionFactory_Leads_To_Exception()
        {
            Assert.Throws<ArgumentException>(() => SqlConnectionFactory.ConnectionFactoryInstance("NServiceBus.SqlServer.UnitTests.SqlConnectionFactoryTests, NServiceBus.SqlServer.UnitTests"));
        }

        [Test]
        public void Class_That_Implements_ISqlConnectionFactory_Works_Ok()
        {
            Assert.IsNotNull(SqlConnectionFactory.ConnectionFactoryInstance("NServiceBus.Transports.SQLServer.DefaultSqlConnectionFactory, NServiceBus.Transports.SQLServer"));
        }
    }
}
