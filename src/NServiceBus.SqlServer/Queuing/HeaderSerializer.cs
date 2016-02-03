namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    static class HeaderSerializer
    {
        static readonly Regex HeaderRegex = new Regex("\"(([^\"]|\\\\\")*)\"\\s*:\\s*(\"((([^\"]|\\\\\")*)\")|(null))\\s*(}|,)", RegexOptions.Compiled);

        internal static string Serialialize(Dictionary<string, string> headers)
        {
            var props = headers.Select(kvp =>
            {
                var value = kvp.Value != null
                    ? $"\"{Escape(kvp.Value)}\""
                    : "null";

                return $"\"{Escape(kvp.Key)}\":{value}";
            });

            return "{" + string.Join(", ", props) + "}";
        }

        static string Escape(string value)
        {
            return value.Replace("\"", "\\\"");
        }

        static string Unescape(string value)
        {
            return value.Replace("\\\"", "\"");
        }

        internal static Dictionary<string, string> Deserialize(string headerString)
        {
            var match = HeaderRegex.Match(headerString);
            var result = new Dictionary<string, string>();
            while (match.Success)
            {
                var key = Unescape(match.Groups[1].Value);
                var value = Unescape(match.Groups[5].Value);
                result[key] = "null".Equals(match.Groups[7].Value, StringComparison.OrdinalIgnoreCase) ? null : value;
                match = match.NextMatch();
            }
            return result;
        } 
    }
}