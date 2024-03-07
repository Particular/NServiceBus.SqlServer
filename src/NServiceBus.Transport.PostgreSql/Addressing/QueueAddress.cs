namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Text;
    using SqlServer;

    class QueueAddress
    {
        public QueueAddress(string table, string schemaName, PostgreSqlNameHelper nameHelper)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Table = table;
            Schema = SafeUnquote(schemaName, nameHelper);
            Value = GetStringForm(nameHelper);
        }

        public string Table { get; }
        public string Schema { get; }
        public string Value { get; }

        //TODO: Update
        //HINT: Algorithm for paring transport addresses runs on few assumptions:
        //      1. Addresses are provided in <table_id>@<schema_id>@<catalog_id> format
        //      2. To preserve compatibility with v2 <table_id> is either:
        //          a. The whole address if no `@` exists in the body of address
        //          b. Prefix of the address up until first `@` from the beginning of the address
        //      3. `@` can be used inside <schema_id> or <catalog_id> only when bracket delimited
        //      4. If the first character of either <schema_id> or <catalog_id> equals to `[`
        //         algorithm assumes that those parts are specified in brackets delimited format
        //      5. Parsing is not eager. If will stop at first `@` that defines correct <schema_id>
        //         or <catalog_id> parts.
        public static QueueAddress Parse(string address, PostgreSqlNameHelper nameHelper)
        {
            var firstAtIndex = address.IndexOf("@", StringComparison.Ordinal);

            if (firstAtIndex == -1)
            {
                return new QueueAddress(address, null, nameHelper);
            }

            var tableName = address.Substring(0, firstAtIndex);
            address = firstAtIndex + 1 < address.Length ? address.Substring(firstAtIndex + 1) : string.Empty;

            ExtractNextPart(address, out var schemaName);
            return new QueueAddress(tableName, schemaName, nameHelper);
        }

        string GetStringForm(PostgreSqlNameHelper nameHelper)
        {
            var result = new StringBuilder();
            var optionalParts = new[] { Schema };
            foreach (var part in optionalParts)
            {
                if (part != null)
                {
                    result.Insert(0, $"@{Quote(part, nameHelper)}");
                }
                else if (result.Length > 0)
                {
                    result.Insert(0, "@[]");
                }
            }
            result.Insert(0, Table);
            return result.ToString();
        }

        static string ExtractNextPart(string address, out string part)
        {
            var noRightBrackets = 0;
            var index = 1;

            while (true)
            {
                if (index >= address.Length)
                {
                    part = address;
                    return string.Empty;
                }

                if (address[index] == '@' && (address[0] != '[' || noRightBrackets % 2 == 1))
                {
                    part = address.Substring(0, index);
                    return index + 1 < address.Length ? address.Substring(index + 1) : string.Empty;
                }

                if (address[index] == ']')
                {
                    noRightBrackets++;
                }

                index++;
            }
        }

        static string Quote(string name, PostgreSqlNameHelper nameHelper)
        {
            return nameHelper.Quote(name);
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