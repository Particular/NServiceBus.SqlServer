namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;
    using NServiceBus.Serializers.Json;

    static class HeaderSerializer
    {
        static JsonMessageSerializer headerSerializer = new JsonMessageSerializer(null);

        internal static string Serialialize(Dictionary<string, string> headers)
        {
            return headerSerializer.SerializeObject(headers);
        }

        internal static Dictionary<string, string> Deserialize(string headerString)
        {
            return (Dictionary<string, string>)headerSerializer.DeserializeObject(headerString, typeof(Dictionary<string, string>));
        } 
    }
}