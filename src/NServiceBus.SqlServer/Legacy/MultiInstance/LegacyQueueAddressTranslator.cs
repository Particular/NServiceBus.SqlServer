namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Concurrent;
    using System.Linq;

    class LegacyQueueAddressTranslator
    {
        public LegacyQueueAddressTranslator(string defaultSchema, string defaultSchemaOverride, QueueSchemaAndCatalogSettings queueSettings)
        {
            Guard.AgainstNullAndEmpty(nameof(defaultSchema), defaultSchema);

            DefaultSchema = string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;
            this.queueSettings = queueSettings ?? new QueueSchemaAndCatalogSettings();
        }

        public string DefaultSchema { get; }

        public QueueAddress Generate(LogicalAddress logicalAddress)
        {
            return logicalAddressCache.GetOrAdd(logicalAddress, TranslateLogicalAddress);
        }

        public LegacyCanonicalQueueAddress Parse(string address)
        {
            return physicalAddressCache.GetOrAdd(address, TranslatePhysicalAddress);
        }

        LegacyCanonicalQueueAddress TranslatePhysicalAddress(string physicalAddress)
        {
            var sqlAddress = QueueAddress.Parse(physicalAddress);

            queueSettings.TryGet(sqlAddress.Table, out var specifiedSchema, out var _); //we ignore catalog

            var schema = specifiedSchema ?? sqlAddress.Schema ?? DefaultSchema;

            return new LegacyCanonicalQueueAddress(sqlAddress.Table, schema);
        }

        static QueueAddress TranslateLogicalAddress(LogicalAddress logicalAddress)
        {
            var nonEmptyParts = new[]
            {
                logicalAddress.EndpointInstance.Endpoint,
                logicalAddress.Qualifier,
                logicalAddress.EndpointInstance.Discriminator
            }.Where(p => !string.IsNullOrEmpty(p));

            var tableName = string.Join(".", nonEmptyParts);


            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out var schemaName);
            var queueAddress = new QueueAddress(tableName, schemaName, null);
            return queueAddress;
        }

        QueueSchemaAndCatalogSettings queueSettings;
        ConcurrentDictionary<string, LegacyCanonicalQueueAddress> physicalAddressCache = new ConcurrentDictionary<string, LegacyCanonicalQueueAddress>();
        ConcurrentDictionary<LogicalAddress, QueueAddress> logicalAddressCache = new ConcurrentDictionary<LogicalAddress, QueueAddress>();
    }
}