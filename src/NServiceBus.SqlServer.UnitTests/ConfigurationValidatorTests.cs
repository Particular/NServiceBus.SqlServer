namespace NServiceBus.SqlServer.UnitTests
{
    using System.Configuration;
    using System.Linq;
    using NUnit.Framework;
    using Transport.SQLServer;

    [TestFixture]
    public class ConfigurationValidatorTests
    {
        Result Validate(params ConnectionStringSettings[] settings)
        {
            var result = new Result();
            string message;

            result.Success = new ConnectionStringsValidator().TryValidate(settings.ToList(), out message);
            result.Message = message;

            return result;
        }

        [Test]
        public void Validation_passes_when_there_is_single_transport_connections_string_without_schema()
        {
            var result = Validate(new ConnectionStringSettings("NServiceBus/Transport", "Source = xxx"));

            Assert.IsTrue(result.Success);
            Assert.IsTrue(string.IsNullOrEmpty(result.Message));
        }

        [Test]
        public void Validation_fails_when_transport_connection_string_has_schema_override()
        {
            var result = Validate(new ConnectionStringSettings("NServiceBus/Transport", "Source = xxx; Queue schema= yyy"));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        [Test]
        public void Validation_fails_when_there_is_more_than_one_transport_connection_string()
        {
            var result = Validate(
                new ConnectionStringSettings("NServiceBus/Transport", "Source = xxx;"),
                new ConnectionStringSettings("NServiceBus/Transport/Endpoint1", "Source = xxx;"));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        [Test]
        public void Validation_passes_when_there_are_more_connection_strings_but_only_one_is_transport()
        {
            var result = Validate(
                new ConnectionStringSettings("NServiceBus/Transport", "Source = xxx;"),
                new ConnectionStringSettings("OtherConnectionString", "Source = xxx;"));

            Assert.IsTrue(result.Success);
            Assert.IsTrue(string.IsNullOrEmpty(result.Message));
        }

        [Test]
        public void Validation_fails_when_only_single_endpoint_specific_transport_connection_string_exists()
        {
            var result = Validate(new ConnectionStringSettings("NServiceBus/Transport/Endpoint1", "Source = xxx;"));

            Assert.IsFalse(result.Success);
            Assert.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        [Test]
        public void Validation_passes_when_there_is_no_transport_connection_string()
        {
            var result = Validate(new ConnectionStringSettings("NoTransport", "Soruce = xxx;"));

            Assert.IsTrue(result.Success);
            Assert.IsTrue(string.IsNullOrEmpty(result.Message));
        }

        class Result
        {
            public bool Success { get; set; }
            public string Message { get; set; }
        }
    }
}