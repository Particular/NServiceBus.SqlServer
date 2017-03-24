namespace NServiceBus.Transport.SQLServer
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

            string specifiedSchema, specifiedCatalog, specifiedInstance;
            queueSettings.TryGet(transportAddress.Table, out specifiedSchema, out specifiedCatalog, out specifiedInstance);

            var instance = specifiedInstance ?? transportAddress.Instance;

            var schema = Override(specifiedSchema, transportAddress.Schema, DefaultSchema, instance == null);
            var catalog = Override(specifiedCatalog, transportAddress.Catalog, DefaultCatalog, instance == null);

            return new CanonicalQueueAddress(transportAddress.Table, schema, catalog, instance);
        }

        static string Override(string configuredValue, string addressValue, string defaultValue, bool isLocal)
        {
            if (isLocal)
            {
                return configuredValue ?? addressValue ?? defaultValue;
            }
            return configuredValue ?? addressValue;
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

            string schemaName, catalogName, instanceName;

            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out schemaName);
            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.CatalogPropertyKey, out catalogName);
            logicalAddress.EndpointInstance.Properties.TryGetValue(SettingsKeys.InstancePropertyKey, out instanceName);

            return new QueueAddress(tableName, schemaName, catalogName, instanceName);
        }
        
        QueueSchemaAndCatalogSettings queueSettings;
        ConcurrentDictionary<string, CanonicalQueueAddress> physicalAddressCache = new ConcurrentDictionary<string, CanonicalQueueAddress>();
        ConcurrentDictionary<LogicalAddress, QueueAddress> logicalAddressCache = new ConcurrentDictionary<LogicalAddress, QueueAddress>();
    }
}