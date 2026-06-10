#nullable enable

namespace NServiceBus.Transport.Sql.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text.Encodings.Web;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    static partial class DictionarySerializer
    {
        public static string Serialize(Dictionary<string, string> dictionary)
            => EscapeDataContractCompatible(JsonSerializer.Serialize(dictionary, Context.DictionaryStringString));

        public static Dictionary<string, string>? Deserialize(string value)
            => JsonSerializer.Deserialize(value, Context.DictionaryStringString);

        static string EscapeDataContractCompatible(string json)
        {
            var span = json.AsSpan();
            var slashCount = span.Count('/');

            if (slashCount == 0)
            {
                return json;
            }

            return string.Create(json.Length + slashCount, json, static (destination, source) =>
            {
                var position = 0;
                foreach (var c in source)
                {
                    if (c == '/')
                    {
                        destination[position++] = '\\';
                    }

                    destination[position++] = c;
                }
            });
        }

        static readonly HeaderSerializationContext Context = new(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        [JsonSerializable(typeof(Dictionary<string, string>))]
        sealed partial class HeaderSerializationContext : JsonSerializerContext;
    }
}