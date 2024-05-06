namespace NServiceBus.Transport.SqlServer
{
    using System;
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
            // AddressKey has a GetHashCode implementation and can be used as a dictionary key, Transport.QueueAddress (from Core) does not (at this time)
            var key = AddressKey.Create(queueAddress);
            return logicalAddressCache.GetOrAdd(key, key => key.ToSqlQueueAddress());
        }

        public CanonicalQueueAddress Parse(string address)
        {
            return physicalAddressCache.GetOrAdd(address, TranslatePhysicalAddress);
        }

        public CanonicalQueueAddress TranslatePhysicalAddress(string address)
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

        public QueueAddress TranslateLogicalAddress(Transport.QueueAddress queueAddress)
        {
            public static AddressKey Create(Transport.QueueAddress a)
            {
                string schema = null;
                string catalog = null;
                if (a.Properties is not null)
                {
                    a.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out schema);
                    a.Properties.TryGetValue(SettingsKeys.CatalogPropertyKey, out catalog);
                }
                return new AddressKey(a.BaseAddress, a.Discriminator, a.Qualifier, schema, catalog);
            }

            public QueueAddress ToSqlQueueAddress()
            {
                var nonEmptyParts = new[]
                {
                    BaseAddress,
                    Qualifier,
                    Discriminator
                }.Where(p => !string.IsNullOrEmpty(p));

                var tableName = string.Join(".", nonEmptyParts);

                return new QueueAddress(tableName, Schema, Catalog);
            }

            return new QueueAddress(tableName, schemaName, catalogName);
        }

        QueueSchemaAndCatalogOptions queueOptions;
        ConcurrentDictionary<string, CanonicalQueueAddress> physicalAddressCache = new ConcurrentDictionary<string, CanonicalQueueAddress>();
        ConcurrentDictionary<Transport.QueueAddress, QueueAddress> logicalAddressCache = new ConcurrentDictionary<Transport.QueueAddress, QueueAddress>();
    }
}