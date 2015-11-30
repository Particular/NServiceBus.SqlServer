namespace NServiceBus.SqlServer.UnitTests
{
    using System.Configuration;
    using System.Linq;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class ConfigurationValidatorTests
    {
        private class Result
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }

        private Result Validate(params ConnectionStringSettings[] settings)
        {
            var result = new Result();
            string message;

            result.Success = new ConfigurationValidator().TryValidate(settings.ToList(), out message);
            result.Message = message;

            return result;
        }

        [Test]
        public void Validation_passes_when_there_is_single_transport_connections_string_without_schema()
        {
            var result = Validate(new ConnectionStringSettings("NServiceBus/Transport", "Soruce = xxx"));

            Assert.IsTrue(result.Success);
            Assert.IsNullOrEmpty(result.Message);
        }

        [Test]
        public void Validation_fails_when_transport_connection_string_has_schema_override()
        {
            var result = Validate(new ConnectionStringSettings("NServiceBus/Transport", "Soruce = xxx; Queue schema= yyy"));

            Assert.IsFalse(result.Success);
            Assert.IsNotNullOrEmpty(result.Message);
        }

        [Test]
        public void Validation_fails_when_there_is_more_than_one_transport_connection_string()
        {
            var result = Validate(
                new ConnectionStringSettings("NServiceBus/Transport", "Soruce = xxx;"),
                new ConnectionStringSettings("NServiceBus/Transport/Endpoint1", "Soruce = xxx;"));

            Assert.IsFalse(result.Success);
            Assert.IsNotNullOrEmpty(result.Message);
        }

        [Test]
        public void Validation_passes_when_there_are_more_connection_strings_but_only_one_is_trasport()
        {
            var result = Validate(
                new ConnectionStringSettings("NServiceBus/Transport", "Soruce = xxx;"),
                new ConnectionStringSettings("OtherConnectionString", "Soruce = xxx;"));

            Assert.IsTrue(result.Success);
            Assert.IsNullOrEmpty(result.Message);
        }

        [Test]
        public void Validation_fails_when_only_single_endpoint_specific_transport_connection_string_exists()
        {
            var result = Validate(new ConnectionStringSettings("NServiceBus/Transport/Endpoint1", "Soruce = xxx;"));

            Assert.IsFalse(result.Success);
            Assert.IsNotNullOrEmpty(result.Message);
        }

        [Test]
        public void Validation_passes_when_there_is_no_transport_connection_string()
        {
            var result = Validate(new ConnectionStringSettings("NoTransport", "Soruce = xxx;"));

            Assert.IsTrue(result.Success);
            Assert.IsNullOrEmpty(result.Message);
        }
    }
}