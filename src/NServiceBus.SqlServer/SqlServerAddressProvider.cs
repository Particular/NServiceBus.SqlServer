namespace NServiceBus.Transports.SQLServer
{
    using System;

    class SqlServerAddressProvider
    {
        readonly string defaultSchema;
        readonly string defaultSchemaOverride;

        Func<string, string> schemaOverrider;

        public string DefaultSchema => string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;

        public SqlServerAddressProvider(string defaultSchema, string defaultSchemaOverride, Func<string, string> schemaOverrider)
        {
            if (string.IsNullOrWhiteSpace(defaultSchema))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultSchema));
            }

            this.defaultSchema = defaultSchema;
            this.defaultSchemaOverride = defaultSchemaOverride;
            this.schemaOverrider = schemaOverrider;
        }

        public SqlServerAddress Parse(string address)
        {
            var sqlAddress = SqlServerAddress.Parse(address);

            if (schemaOverrider != null)
            {
                var schemaOverride = schemaOverrider(sqlAddress.TableName);

                if (string.IsNullOrWhiteSpace(schemaOverride) == false)
                {
                    return new SqlServerAddress(sqlAddress.TableName, schemaOverride);
                }
            }

            if (string.IsNullOrWhiteSpace(sqlAddress.SchemaName) == false)
            {
                return sqlAddress;
            }

            if (string.IsNullOrWhiteSpace(defaultSchemaOverride) == false)
            {
                return new SqlServerAddress(sqlAddress.TableName, defaultSchemaOverride);
            }

            return new SqlServerAddress(sqlAddress.TableName, defaultSchema);
        }     
    }
}