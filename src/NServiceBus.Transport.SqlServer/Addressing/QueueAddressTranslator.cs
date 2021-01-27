namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Concurrent;
    using System.Linq;

    class QueueAddressTranslator
    {
        public QueueAddressTranslator(string defaultCatalog, string defaultSchema, string defaultSchemaOverride, QueueSchemaAndCatalogSettings queueSettings)
        {
            Guard.AgainstNullAndEmpty(nameof(defaultSchema), defaultSchema);

            DefaultCatalog = defaultCatalog;
            DefaultSchema = string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;
            this.queueSettings = queueSettings ?? new QueueSchemaAndCatalogSettings();
        }

        public string DefaultCatalog { get; }

        public string DefaultSchema { get; }

        public QueueAddress Generate(LogicalAddress logicalAddress)
        {
            return logicalAddressCache.GetOrAdd(logicalAddress, TranslateLogicalAddress);
        }

        public CanonicalQueueAddress Parse(string address)
        {
            return physicalAddressCache.GetOrAdd(address, TranslatePhysicalAddress);
        }

        CanonicalQueueAddress TranslatePhysicalAddress(string address)
        {
            var transportAddress = QueueAddress.Parse(address);

            return GetCanonicalForm(transportAddress);
        }

        public CanonicalQueueAddress GetCanonicalForm(QueueAddress transportAddress)
        {
            queueSettings.TryGet(transportAddress.Table, out var specifiedSchema, out var specifiedCatalog);

            var schema = Override(specifiedSchema, transportAddress.Schema, DefaultSchema);
            var catalog = Override(specifiedCatalog, transportAddress.Catalog, DefaultCatalog);

            return new CanonicalQueueAddress(transportAddress.Table, schema, catalog);
        }

        static string Override(string configuredValue, string addressValue, string defaultValue)
        {
            return configuredValue ?? addressValue ?? defaultValue;
        }

        public static QueueAddress TranslateLogicalAddress(LogicalAddress logicalAddress)
        {
            var nonEmptyParts = new[]
            {
                logicalAddress.EndpointInstance.Endpoint,
                logicalAddress.Qualifier,
                logicalAddress.EndpointInstance.Discriminator
            }.Where(p => !string.IsNullOrEmpty(p));

            var tableName = string.Join(".", nonEmptyParts);


            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out var schemaName);
            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.CatalogPropertyKey, out var catalogName);

            return new QueueAddress(tableName, schemaName, catalogName);
        }

        QueueSchemaAndCatalogSettings queueSettings;
        ConcurrentDictionary<string, CanonicalQueueAddress> physicalAddressCache = new ConcurrentDictionary<string, CanonicalQueueAddress>();
        ConcurrentDictionary<LogicalAddress, QueueAddress> logicalAddressCache = new ConcurrentDictionary<LogicalAddress, QueueAddress>();
    }
}