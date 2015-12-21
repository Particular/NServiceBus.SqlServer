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

                return new QueueAddress(tableName, schemaName);
            }

            return new QueueAddress(address, null);
        }

        public override string ToString()
        {
            return $"{TableName}@{SchemaName}";
        }
    }
}