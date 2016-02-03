namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.Serialization.Formatters;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    static class HeaderSerializer
    {
        static JsonSerializerSettings serializerSettings = new JsonSerializerSettings
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
        internal static string Serialize(Dictionary<string, string> headers)
        {
            return JsonConvert.SerializeObject(headers, Formatting.None, serializerSettings);
        }

        internal static Dictionary<string, string> Deserialize(string headerString)
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(headerString, serializerSettings);
        }
    }
}