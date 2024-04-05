namespace NServiceBus.Transport.PostgreSql
{
    using SqlServer;

    class QueueAddress
    {
        public QueueAddress(string table, string schemaName, PostgreSqlNameHelper nameHelper)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Table = SafeUnquote(table, nameHelper);
            Schema = SafeUnquote(schemaName, nameHelper);
        }

        public string Table { get; }
        public string Schema { get; }

        public static QueueAddress Parse(string address, PostgreSqlNameHelper nameHelper)
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

                    return new QueueAddress(table, schema, nameHelper);
                }
                index++;
            }

            return new QueueAddress(address, null, nameHelper);
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