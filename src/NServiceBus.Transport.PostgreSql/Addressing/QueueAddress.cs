﻿namespace NServiceBus.Transport.PostgreSql
{
    using System;

    class QueueAddress
    {
        public QueueAddress(string table, string schemaName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(table);
            Table = SafeUnquote(table);
            Schema = SafeUnquote(schemaName);
        }

        public string Table { get; }
        public string Schema { get; }

        public static QueueAddress Parse(string address)
        {
            /*
             * The address format is two quoted identifiers joined by the @ character e.g. "table"@"schema".
             * 
             */
            var index = 0;
            var quoteCount = 0;
            while (index < address.Length)
            {
                if (address[index] == '"')
                {
                    quoteCount++;
                }
                else if (address[index] == '.' && quoteCount % 2 == 0)
                {
                    var schema = address.Substring(0, index);
                    var table = address.Substring(index + 1);

                    return new QueueAddress(table, schema);
                }
                index++;
            }

            return new QueueAddress(address, null);
        }

        static string SafeUnquote(string name)
        {
            var result = PostgreSqlNameHelper.Unquote(name);
            return string.IsNullOrWhiteSpace(result)
                ? null
                : result;
        }
    }
}