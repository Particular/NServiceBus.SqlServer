namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Json;
    using System.Text;

    static class DictionarySerializer
    {
        public static string Serialize(Dictionary<string, string> instance)
        {
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, instance);
                return Encoding.Default.GetString(stream.ToArray());
            }
        }

        public static Dictionary<string, string> DeSerialize(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, string>));
            using (var stream = new MemoryStream(Encoding.Default.GetBytes(json)))
            {
                return (Dictionary<string, string>)serializer.ReadObject(stream);
            }
        }
    }
}