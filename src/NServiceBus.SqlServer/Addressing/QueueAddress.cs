namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class QueueAddress
    {
        public QueueAddress(string tableName, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);

            TableName = tableName;
            SchemaName = UnescapeIdentifier(schemaName);
        }

        public string TableName { get; }
        public string SchemaName { get; }

        //HINT: Algorithm for paring transport addresses runs on few assumptions:
        //      1. Addresses are provided in <table_id>@<schema_id> format
        //      2. To preserve compatibility with v2 <table_id> is either:
        //          a. The whole address if no `@` exists in the body of address
        //          b. Prefix of the address up until first `@` from the beginning of the address
        //      3. `@` can be used inside <schema_id> only when bracket delimited
        //      4. If the first character of <schema_id> equals to `[`
        //         algorithm assumes that those parts are specified in brackets delimited format
        //      5. Parsing is not eager. If will stop at first `@` that defines correct <schema_id> part.
        public static QueueAddress Parse(string address)
        {
            var firstAtIndex = address.IndexOf("@", StringComparison.Ordinal);

            if (firstAtIndex == -1)
            {
                return new QueueAddress(address, null);
            }

            var tableName = address.Substring(0, firstAtIndex);
            address = firstAtIndex + 1 < address.Length ? address.Substring(firstAtIndex + 1) : string.Empty;

            var schemaName = ExtractSchema(address);
            return new QueueAddress(tableName, schemaName);
        }

        static string ExtractSchema(string address)
        {
            var noRightBrackets = 0;
            var index = 1;

            while (true)
            {
                if (index >= address.Length)
                {
                    return address;
                }
                if (address[index] == '@' && (address[0] != '[' || noRightBrackets % 2 == 1))
                {
                    return address.Substring(0, index);
                }

                if (address[index] == ']')
                {
                    noRightBrackets++;
                }
                index++;
            }
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(SchemaName))
            {
                return $"{TableName}@[{SchemaName}]";
            }

            return TableName;
        }

        static string UnescapeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                return identifier;
            }

            using (var sanitizer = new SqlCommandBuilder())
            {
                return sanitizer.UnquoteIdentifier(identifier);
            }
        }
    }
}