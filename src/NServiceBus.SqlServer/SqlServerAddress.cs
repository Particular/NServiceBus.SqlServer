namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Linq;

    class SqlServerAddress
    {
        public string TableName { get; private set; }
        public string SchemaName { get; private set; }

        public SqlServerAddress(string tableName, string schemaName)
        {
            if (string.IsNullOrWhiteSpace(tableName))
            {
                throw new ArgumentOutOfRangeException(nameof(tableName));
            }

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

        public static SqlServerAddress Parse(string address)
        {
            if (address.Contains("@"))
            {
                var parts = address.Split('@');
                var tableName = parts[0];
                var schemaName = parts[1];

                return new SqlServerAddress(tableName, schemaName);
            }

            return new SqlServerAddress(address, null);
        }

        public override string ToString()
        {
            return $"{TableName}@{SchemaName}";
        }
    }
}