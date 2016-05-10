namespace NServiceBus.Transport.SQLServer
{
    using System.Text.RegularExpressions;

    class QueueAddress
    {
        public QueueAddress(string tableName, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);

            TableName = tableName;
            SchemaName = UnescapeSchema(schemaName);
        }

        public string TableName { get; }
        public string SchemaName { get; }

        public static QueueAddress Parse(string address)
        {
            if (address.Contains("@"))
            {
                var parts = address.Split('@');
                var tableName = parts[0];
                var schemaName = parts[1];

                return new QueueAddress(tableName, schemaName);
            }

            return new QueueAddress(address, null);
        }

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(SchemaName))
            {
                return $"{TableName}@[{SchemaName}]";
            }

            return TableName;
        }

        static string UnescapeSchema(string schemaName)
        {
            if (IsEscapedSchema(schemaName))
            {
                return schemaName.Substring(1, schemaName.Length - 2);
            }

            return schemaName;
        }

        static bool IsEscapedSchema(string schema)
        {
            if (schema == null)
            {
                return false;
            }

            return Regex.IsMatch(schema, "^\\[(.*)\\]$");
        }
    }
}