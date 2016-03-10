namespace NServiceBus.Transports.SQLServer
{
    class QueueAddress
    {
        public string TableName { get; }
        public string SchemaName { get; }

        public QueueAddress(string tableName, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(tableName), tableName);

            TableName = tableName;
            SchemaName = schemaName;
        }
        
        public static QueueAddress Parse(string address)
        {
            if (address.Contains("@"))
            {
                var parts = address.Split('@');
                var tableName = parts[0];
                var schemaName = parts[1];
                if (schemaName.StartsWith("[") && schemaName.EndsWith("]"))
                {
                    schemaName = schemaName.Substring(1, schemaName.Length - 2);
                }

                return new QueueAddress(tableName, schemaName);
            }

            return new QueueAddress(address, null);
        }

        public override string ToString()
        {
            var escapedSchema = SchemaName ?? "";
            if (!string.IsNullOrWhiteSpace(escapedSchema) && !(escapedSchema.StartsWith("[") && escapedSchema.EndsWith("]")))
            {
                escapedSchema = $"[{escapedSchema}]";
            }
            if (!string.IsNullOrWhiteSpace(escapedSchema))
                escapedSchema = "@" + escapedSchema;
            return $"{TableName}{escapedSchema}";
        }
    }
}