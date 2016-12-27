namespace NServiceBus.Transport.SQLServer
{
    class QueueAddressParser
    {
        public QueueAddressParser(string defaultSchema, string defaultSchemaOverride, TableSchemasSettings tableSchemaSettings)
        {
            Guard.AgainstNullAndEmpty(nameof(defaultSchema), defaultSchema);

            this.defaultSchema = defaultSchema;
            this.defaultSchemaOverride = defaultSchemaOverride;
            this.tableSchemaSettings = tableSchemaSettings ?? new TableSchemasSettings();
        }

        public string DefaultSchema => string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;

        public QueueAddress Parse(string address)
        {
            var sqlAddress = QueueAddress.Parse(address);

            string schema;
            if (tableSchemaSettings.TryGet(sqlAddress.TableName, out schema))
            {
                return sqlAddress.OverrideSchema(schema);
            }

            return sqlAddress.HasDefinedSchema 
                ? sqlAddress 
                : sqlAddress.OverrideSchema(DefaultSchema);
        }

        string defaultSchema;
        string defaultSchemaOverride;
        TableSchemasSettings tableSchemaSettings;
    }
}