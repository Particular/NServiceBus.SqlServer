namespace NServiceBus.Transports.SQLServer
{
    using System.Text.RegularExpressions;

    class QueueAddress
    {
        public string TableName { get; }
        public string SchemaName { get; }

        public QueueAddress(string tableName, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);

            TableName = tableName;
            SchemaName = UnescapeSchema(schemaName);
        }
        
        public static QueueAddress Parse(string address)
        {
            if (address.Contains("@"))
            {
                var parts = address.Split('@');
                var tableName = parts[0];
                var schemaName = UnescapeSchema(parts[1]);

                return new QueueAddress(tableName, schemaName);
            }

            return new QueueAddress(address, null);
        }

        public override string ToString()
        {
            var escapedSchema = SchemaName ?? "";
            if (!string.IsNullOrWhiteSpace(escapedSchema) && !IsEscapedSchema(escapedSchema))
            {
                escapedSchema = $"[{escapedSchema}]";
            }
            if (!string.IsNullOrWhiteSpace(escapedSchema))
                escapedSchema = "@" + escapedSchema;
            return $"{TableName}{escapedSchema}";
        }

        private static string UnescapeSchema(string schemaName)
        {
            if (schemaName == null) return null;
            if (IsEscapedSchema(schemaName))
            {
                return schemaName.Substring(1, schemaName.Length - 2);
            }
            return schemaName;
        }

        private static bool IsEscapedSchema(string schema)
        {
            if (schema == null) return true;
            return Regex.IsMatch(schema, "^\\[(.*)\\]$");
        }
    }
}