namespace NServiceBus.SqlServer.UnitTests
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.Serialization.Formatters;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    [TestFixture]
    public class HeaderSerializerTests
    {
        static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
        {
            { "SimpleKey", "SimpleValue"},
            { "lowercaseKey", "lowercaseValue"},
            { "qote\"ke'y", "quote\"valu'e"},
            { "paren}key{}", "parent}value{}"},
        };

        [Test]
        public void Roudtrip_test_various_case()
        {
            var headersDeserialized = HeaderSerializer.Deserialize(HeaderSerializer.Serialialize(Expected));
            CollectionAssert.AreEquivalent(Expected, headersDeserialized);
        }

        [Test]
        public void Test_compatibility_with_Newtonsoft()
        {
            var serializerSettings = new JsonSerializerSettings
            {
                TypeNameAssemblyFormat = FormatterAssemblyStyle.Simple,
                TypeNameHandling = TypeNameHandling.Auto,
                Converters =
                {
                    new IsoDateTimeConverter
                    {
                        DateTimeStyles = DateTimeStyles.RoundtripKind
                    }
                }
            };

            var headersDeserialized = HeaderSerializer.Deserialize(JsonConvert.SerializeObject(Expected, Formatting.None, serializerSettings));
            CollectionAssert.AreEquivalent(Expected, headersDeserialized);

            headersDeserialized = (Dictionary<string, string>)JsonConvert.DeserializeObject(HeaderSerializer.Serialialize(Expected), typeof(Dictionary<string, string>));
            CollectionAssert.AreEquivalent(Expected, headersDeserialized);
        }
    }
}