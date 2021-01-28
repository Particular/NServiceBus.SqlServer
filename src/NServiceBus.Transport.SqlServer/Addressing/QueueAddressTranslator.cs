namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Concurrent;
    using System.Linq;

    class QueueAddressTranslator
    {
        public QueueAddressTranslator(string defaultCatalog, string defaultSchema, string defaultSchemaOverride, QueueSchemaAndCatalogOptions queueOptions)
        {
            Guard.AgainstNullAndEmpty(nameof(defaultSchema), defaultSchema);

            DefaultCatalog = defaultCatalog;
            DefaultSchema = string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;
            this.queueOptions = queueOptions ?? new QueueSchemaAndCatalogOptions();
        }

        public string DefaultCatalog { get; }

        public string DefaultSchema { get; }

        public QueueAddress Generate(Transport.QueueAddress queueAddress)
        {
            return logicalAddressCache.GetOrAdd(queueAddress, TranslateLogicalAddress);
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
            queueOptions.TryGet(transportAddress.Table, out var specifiedSchema, out var specifiedCatalog);

            var schema = Override(specifiedSchema, transportAddress.Schema, DefaultSchema);
            var catalog = Override(specifiedCatalog, transportAddress.Catalog, DefaultCatalog);

            return new CanonicalQueueAddress(transportAddress.Table, schema, catalog);
        }

        static string Override(string configuredValue, string addressValue, string defaultValue)
        {
            return configuredValue ?? addressValue ?? defaultValue;
        }

        public static QueueAddress TranslateLogicalAddress(Transport.QueueAddress queueAddress)
        {
            var nonEmptyParts = new[]
            {
                queueAddress.BaseAddress,
                queueAddress.Qualifier,
                queueAddress.Discriminator
            }.Where(p => !string.IsNullOrEmpty(p));

            var tableName = string.Join(".", nonEmptyParts);


            string schemaName = null;
            string catalogName = null;

            if (queueAddress.Properties != null)
            {
                queueAddress?.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out schemaName);
                queueAddress?.Properties.TryGetValue(SettingsKeys.CatalogPropertyKey, out catalogName);
            }

            return new QueueAddress(tableName, schemaName, catalogName);
        }

        QueueSchemaAndCatalogOptions queueOptions;
        ConcurrentDictionary<string, CanonicalQueueAddress> physicalAddressCache = new ConcurrentDictionary<string, CanonicalQueueAddress>();
        ConcurrentDictionary<Transport.QueueAddress, QueueAddress> logicalAddressCache = new ConcurrentDictionary<Transport.QueueAddress, QueueAddress>();
    }
}