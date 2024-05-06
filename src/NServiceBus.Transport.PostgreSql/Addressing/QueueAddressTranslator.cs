namespace NServiceBus.Transport.PostgreSql
{
    using System.Collections.Concurrent;
    using System.Linq;
    using Configuration;

    class QueueAddressTranslator
    {
        public QueueAddressTranslator(string defaultSchema, string defaultSchemaOverride, QueueSchemaOptions queueOptions)
        {
            Guard.AgainstNullAndEmpty(nameof(defaultSchema), defaultSchema);

            DefaultSchema = string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;
            this.queueOptions = queueOptions ?? new QueueSchemaOptions();
        }

        public string DefaultSchema { get; }

        public QueueAddress Generate(Transport.QueueAddress queueAddress)
        {
            return logicalAddressCache.GetOrAdd(queueAddress, TranslateLogicalAddress);
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
            queueOptions.TryGet(transportAddress.Table, out var specifiedSchema);

            var schema = Override(specifiedSchema, transportAddress.Schema, DefaultSchema);

            return new CanonicalQueueAddress(transportAddress.Table, schema);
        }

        static string Override(string configuredValue, string addressValue, string defaultValue)
        {
            return configuredValue ?? addressValue ?? defaultValue;
        }

        public QueueAddress TranslateLogicalAddress(Transport.QueueAddress queueAddress)
        {
            var nonEmptyParts = new[]
            {
                queueAddress.BaseAddress,
                queueAddress.Qualifier,
                queueAddress.Discriminator
            }.Where(p => !string.IsNullOrEmpty(p));

            var tableName = string.Join(".", nonEmptyParts);


            string schemaName = null;

            if (queueAddress.Properties != null)
            {
                queueAddress?.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out schemaName);
            }

            return new QueueAddress(tableName, schemaName);
        }

        QueueSchemaOptions queueOptions;
        ConcurrentDictionary<string, CanonicalQueueAddress> physicalAddressCache = new ConcurrentDictionary<string, CanonicalQueueAddress>();
        ConcurrentDictionary<Transport.QueueAddress, QueueAddress> logicalAddressCache = new ConcurrentDictionary<Transport.QueueAddress, QueueAddress>();
    }
}