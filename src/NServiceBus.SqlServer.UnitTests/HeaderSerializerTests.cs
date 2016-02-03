namespace NServiceBus.SqlServer.UnitTests
{
    using System.Collections.Generic;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class HeaderSerializerTests
    {
        [Test]
        public void Do()
        {
            var headers = new Dictionary<string, string>
            {
                { "SimpleKey", "SimpleValue"},
                { "lowercaseKey", "lowercaseValue"},
                { "qote\"ke'y", "quote\"valu'e"},
                { "paren}key{}", "parent}value{}"},
            };

            var headersDeserialized = HeaderSerializer.Deserialize(HeaderSerializer.Serialialize(headers));

            CollectionAssert.AreEquivalent(headers, headersDeserialized);
        }
    }
}