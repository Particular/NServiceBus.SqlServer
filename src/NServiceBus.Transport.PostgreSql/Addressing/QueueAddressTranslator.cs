namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using Configuration;

    class QueueAddressTranslator
    {
        public QueueAddressTranslator(string defaultSchema, string defaultSchemaOverride, QueueSchemaOptions queueOptions)
        {
            //TODO: check if we can migrate from Guard classes to ArgumentException-like equivalents
            ArgumentException.ThrowIfNullOrWhiteSpace(defaultSchema);

            DefaultSchema = string.IsNullOrWhiteSpace(defaultSchemaOverride) ? defaultSchema : defaultSchemaOverride;
            this.queueOptions = queueOptions ?? new QueueSchemaOptions();
        }

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
            queueOptions.TryGet(transportAddress.Table, out var specifiedSchema);

            var schema = Override(specifiedSchema, transportAddress.Schema, DefaultSchema);

            return new CanonicalQueueAddress(transportAddress.Table, schema);
        }

        static string Override(string configuredValue, string addressValue, string defaultValue)
        {
            return configuredValue ?? addressValue ?? defaultValue;
        }
        
        readonly QueueSchemaOptions queueOptions;
        readonly ConcurrentDictionary<AddressKey, QueueAddress> logicalAddressCache = new();
        readonly ConcurrentDictionary<string, CanonicalQueueAddress> physicalAddressCache = new();
        
        record struct AddressKey(string BaseAddress, string Discriminator, string Qualifier, string Schema)
        {
            public static AddressKey Create(Transport.QueueAddress a)
            {
                string schema = null;
                if (a.Properties is not null)
                {
                    a.Properties.TryGetValue(SettingsKeys.SchemaPropertyKey, out schema);
                }
                return new AddressKey(a.BaseAddress, a.Discriminator, a.Qualifier, schema);
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

                return new QueueAddress(tableName, Schema);
            }
        }
    }
}