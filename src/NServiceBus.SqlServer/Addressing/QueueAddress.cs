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

        //HINT: Algorithm for parsing transport addresses runs with few assumptions:
        //      1. Addresses are provided in <table_id>@<schema_id> format
        //      2. To preserve compatibility with v2 <table_id> is either:
        //          a. The whole address if no `@` exists in the body of the address
        //          b. Prefix of the address until first occurrence of `@`
        //      3. `@` can be used inside <schema_id> only when bracket delimited
        //      4. If the first character of <schema_id> equals left bracket (`[`)
        //         algorithm assumes that schema part is in brackets delimited format
        //      5. Parsing does not assume the address needs to end with schema part.
        //         It will stop at first `@` that defines correct <schema_id> part.
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

        bool Equals(QueueAddress other)
        {
            return string.Equals(TableName, other.TableName) && string.Equals(SchemaName, other.SchemaName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((QueueAddress) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((TableName != null ? TableName.GetHashCode() : 0)*397) ^ (SchemaName != null ? SchemaName.GetHashCode() : 0);
            }
        }
    }
}