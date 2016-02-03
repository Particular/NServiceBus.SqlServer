namespace NServiceBus.Transports.SQLServer
{
    using System;

    class QueueAddressParser
    {
        readonly string defaultSchema;
        readonly string defaultSchemaOverride;

        Func<string, string> schemaOverrider;

        public string DefaultSchema => string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;

        public QueueAddressParser(string defaultSchema, string defaultSchemaOverride, Func<string, string> schemaOverrider)
        {
            Guard.AgainstNullAndEmpty(nameof(defaultSchema), defaultSchema);

            this.defaultSchema = defaultSchema;
            this.defaultSchemaOverride = defaultSchemaOverride;
            this.schemaOverrider = schemaOverrider;
        }

        public QueueAddress Parse(string address)
        {
            var sqlAddress = QueueAddress.Parse(address);

            if (schemaOverrider != null)
            {
                var schemaOverride = schemaOverrider(sqlAddress.TableName);

                if (string.IsNullOrWhiteSpace(schemaOverride) == false)
                {
                    return new QueueAddress(sqlAddress.TableName, schemaOverride);
                }
            }

            if (string.IsNullOrWhiteSpace(sqlAddress.SchemaName) == false)
            {
                return sqlAddress;
            }

            if (string.IsNullOrWhiteSpace(defaultSchemaOverride) == false)
            {
                return new QueueAddress(sqlAddress.TableName, defaultSchemaOverride);
            }

            return new QueueAddress(sqlAddress.TableName, defaultSchema);
        }     
    }
}