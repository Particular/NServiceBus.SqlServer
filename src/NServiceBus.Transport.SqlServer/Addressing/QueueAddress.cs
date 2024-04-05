namespace NServiceBus.Transport.SqlServer
{
    using System;
    using System.Text;

    class QueueAddress
    {
        public QueueAddress(string table, string schemaName, string catalogName, SqlServerNameHelper nameHelper)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(table);

            Table = table;
            Catalog = SafeUnquote(catalogName, nameHelper);
            Schema = SafeUnquote(schemaName, nameHelper);
            Value = GetStringForm(nameHelper);
        }

        public string Catalog { get; }
        public string Table { get; }
        public string Schema { get; }
        public string Value { get; }

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



        // table@[db@]@[catalog] ->
        public static QueueAddress Parse(string address, SqlServerNameHelper nameHelper)
        {
            var firstAtIndex = address.IndexOf("@", StringComparison.Ordinal);

            if (firstAtIndex == -1)
            {
                return new QueueAddress(address, null, null, nameHelper);
            }

            var tableName = address.Substring(0, firstAtIndex);
            address = firstAtIndex + 1 < address.Length ? address.Substring(firstAtIndex + 1) : string.Empty;

            address = ExtractNextPart(address, out var schemaName);

            string catalogName = null;

            if (address != string.Empty)
            {
                ExtractNextPart(address, out catalogName);
            }
            return new QueueAddress(tableName, schemaName, catalogName, nameHelper);
        }

        string GetStringForm(SqlServerNameHelper nameHelper)
        {
            var result = new StringBuilder();
            var optionalParts = new[] { Catalog, Schema };
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

        static string Quote(string name, SqlServerNameHelper nameHelper)
        {
            return nameHelper.Quote(name);
        }

        static string SafeUnquote(string name, SqlServerNameHelper nameHelper)
        {
            var result = nameHelper.Unquote(name);
            return string.IsNullOrWhiteSpace(result)
                ? null
                : result;
        }
    }
}