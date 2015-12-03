namespace NServiceBus.Transports.SQLServer
{
    using System.Linq;

    class QueueAddress
    {
        public string TableName { get; private set; }
        public string SchemaName { get; private set; }

        public QueueAddress(string tableName, string schemaName)
        {
            Guard.AgainstNullAndEmpty("tableName", tableName);

            TableName = tableName;
            SchemaName = schemaName;
        }

        //TODO: this does not belong here :/
        public static string ToTransportAddressText(LogicalAddress logicalAddress)
        {
            var endpointNamePart = logicalAddress.EndpointInstanceName.EndpointName.ToString();
            var qualifierPart = logicalAddress.Qualifier;
            var userDiscriminatorPart = logicalAddress.EndpointInstanceName.UserDiscriminator;

            var nonEmptyParts = new[]
            {
                endpointNamePart,
                qualifierPart,
                userDiscriminatorPart
            }.Where(p => !string.IsNullOrEmpty(p));

            var tableName = string.Join(".", nonEmptyParts);
            var schemaName = logicalAddress.EndpointInstanceName.TransportDiscriminator;

            var address = string.IsNullOrWhiteSpace(schemaName) ? tableName : $"{tableName}@{schemaName}";

            return address;
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