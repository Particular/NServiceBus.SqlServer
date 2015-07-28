namespace NServiceBus.SqlServer.UnitTests
{
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class ConnectionStringParserTests
    {
        [Test]
        public void Should_extract_expected_schema_name_from_connection_string()
        {
            const string expected = "test_schema";
            const string test_connection_string = @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus;Integrated Security=True; Queue Schema=test_schema";

            var info = ConnectionStringParser.AsConnectionInfo(test_connection_string);

            Assert.AreEqual(expected, info.SchemaName);
        }

        [Test]
        public void Schema_name_shopuld_default_to_dbo_if_not_specified()
        {
            const string expected = "dbo";
            const string test_connection_string = @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus;Integrated Security=True;";

            var info = ConnectionStringParser.AsConnectionInfo( test_connection_string );

            Assert.AreEqual( expected, info.SchemaName );
        }

        [Test]
        public void Should_extract_expected_connection_string()
        {
            const string expected = @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus;Integrated Security=True";
            const string test_connection_string = @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus;Integrated Security=True; Queue Schema=test_schema";

            var info = ConnectionStringParser.AsConnectionInfo( test_connection_string );

            Assert.AreEqual( expected.ToLowerInvariant(), info.ConnectionString.ToLowerInvariant() );
        }
    }
}