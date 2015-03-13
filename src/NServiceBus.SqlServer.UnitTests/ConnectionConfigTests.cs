namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using NServiceBus.Transports.SQLServer;
    using NServiceBus.Transports.SQLServer.Config;
    using NUnit.Framework;

    [TestFixture]
    class ConnectionConfigTests : ConfigTest
    {
        [Test]
        public void Config_file_connection_string_convention_has_precedence_over_code_configured_endpoint_connection_info()
        {
            var busConfig = new BusConfiguration();
            busConfig.UseTransport<SqlServerTransport>().UseSpecificConnectionInformation(
                EndpointConnectionInfo.For("Endpoint1").UseConnectionString("Source=Code").UseSchema("CodeSchema"),
                EndpointConnectionInfo.For("Endpoint2").UseConnectionString("Source=Code").UseSchema("CodeSchema"));

            var connectionConfig = new ConnectionConfig(new List<ConnectionStringSettings>()
            {
                new ConnectionStringSettings("NServiceBus/Transport/Endpoint1", "Source=Config; Queue Schema=ConfigSchema")
            });

            var builder = Activate(busConfig, connectionConfig);
            var connectionProvider = builder.Build<IConnectionStringProvider>();

            //Config
            var connectionParams = connectionProvider.GetForDestination(Address.Parse("Endpoint1"));
            Assert.IsTrue("Source=Config".Equals(connectionParams.ConnectionString, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual("ConfigSchema", connectionParams.Schema);

            //Fallback - code
            connectionParams = connectionProvider.GetForDestination(Address.Parse("Endpoint2"));
            Assert.IsTrue("Source=Code".Equals(connectionParams.ConnectionString, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual("CodeSchema", connectionParams.Schema);
        }

        [Test]
        public void Code_configured_connection_string_provided_as_collection_has_precedence_over_one_provided_via_transport_connection_string()
        {
            var busConfig = new BusConfiguration();
            busConfig.UseTransport<SqlServerTransport>().UseSpecificConnectionInformation(
                EndpointConnectionInfo.For("Endpoint1").UseConnectionString("Source=Code").UseSchema("CodeSchema"));

            var builder = Activate(busConfig, new ConnectionConfig(new List<ConnectionStringSettings>()), "Source=Default; Queue Schema=DefaultSchema");
            var connectionProvider = builder.Build<IConnectionStringProvider>();

            //Code
            var connectionParams = connectionProvider.GetForDestination(Address.Parse("Endpoint1"));
            Assert.AreEqual("Source=Code", connectionParams.ConnectionString);
            Assert.AreEqual("CodeSchema", connectionParams.Schema);

            //Fallback - default
            connectionParams = connectionProvider.GetForDestination(Address.Parse("Endpoint2"));
            Assert.IsTrue("Source=Default".Equals(connectionParams.ConnectionString, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual("DefaultSchema", connectionParams.Schema);
        }

        [Test]
        public void Code_configured_connection_string_provided_by_callback_has_precedence_over_one_provided_via_transport_connection_string()
        {
            var busConfig = new BusConfiguration();
            busConfig.UseTransport<SqlServerTransport>().UseSpecificConnectionInformation(
                e => e == "Endpoint1" ? ConnectionInfo.Create().UseConnectionString("Source=Code").UseSchema("CodeSchema") : null);

            var builder = Activate(busConfig, new ConnectionConfig(new List<ConnectionStringSettings>()), "Source=Default; Queue Schema=DefaultSchema");
            var connectionProvider = builder.Build<IConnectionStringProvider>();

            //Code 
            var connectionParams = connectionProvider.GetForDestination(Address.Parse("Endpoint1"));
            Assert.AreEqual("Source=Code", connectionParams.ConnectionString);
            Assert.AreEqual("CodeSchema", connectionParams.Schema);

            //Fallback - default
            connectionParams = connectionProvider.GetForDestination(Address.Parse("Endpoint2"));
            Assert.IsTrue("Source=Default".Equals(connectionParams.ConnectionString, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual("DefaultSchema", connectionParams.Schema);
        }

        [Test]
        public void Schema_provided_via_transport_connection_string_has_precedence_over_code_configured()
        {
            var busConfig = new BusConfiguration();
            busConfig.UseTransport<SqlServerTransport>().DefaultSchema("CodeSchema");

            var builder = Activate(busConfig, new ConnectionConfig(new List<ConnectionStringSettings>()), "Source=Default;Queue Schema=DefaultSchema");
            var connectionProvider = builder.Build<IConnectionStringProvider>();

            Assert.AreEqual("DefaultSchema", connectionProvider.GetForDestination(Address.Parse("Endpoint")).Schema);
        }

        [Test]
        public void Connection_info_can_not_be_specified_both_via_collection_and_callback()
        {
            var busConfig = new BusConfiguration();
            var transportConfig = busConfig.UseTransport<SqlServerTransport>();
            transportConfig.UseSpecificConnectionInformation(e => null);

            Assert.Throws<InvalidOperationException>(() => transportConfig.UseSpecificConnectionInformation(e => null));
            Assert.Throws<InvalidOperationException>(() => transportConfig.UseSpecificConnectionInformation(new List<EndpointConnectionInfo>()));
        }
    }
}