namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using SqlServer;

    class QueueAddress
    {
        public QueueAddress(string table, string schemaName, PostgreSqlNameHelper nameHelper)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Table = table;
            Schema = SafeUnquote(schemaName, nameHelper);
        }

        public string Table { get; }
        public string Schema { get; }

        public static QueueAddress Parse(string address, PostgreSqlNameHelper nameHelper)
        {
            var firstAtIndex = address.IndexOf("@", StringComparison.Ordinal);

            if (firstAtIndex == -1)
            {
                return new QueueAddress(address, null, nameHelper);
            }

            var table = address.Substring(0, firstAtIndex);
            var schema = address.Substring(firstAtIndex + 1);
            return new QueueAddress(table, schema, nameHelper);
        }

        static string SafeUnquote(string name, PostgreSqlNameHelper nameHelper)
        {
            var result = nameHelper.Unquote(name);
            return string.IsNullOrWhiteSpace(result)
                ? null
                : result;
        }
    }
}