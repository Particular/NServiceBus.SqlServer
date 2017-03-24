namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Data.SqlClient;

    class CanonicalQueueAddress
    {
        string qualifiedTableName;

        public CanonicalQueueAddress(string table, string schemaName, string catalogName, string instanceName)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            if (instanceName == null) //Only allow empty schema and catalog for remote queues
            {
                Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);
                Guard.AgainstNullAndEmpty(nameof(catalogName), catalogName);
            }
            Table = table;
            Catalog = catalogName;
            Schema = schemaName;
            Instance = instanceName;
            Address = GetCanonicalForm();
            if (instanceName == null) //Only set the qualified queue name for local queues
            {
                qualifiedTableName = $"{Quote(Catalog)}.{Quote(Schema)}.{Quote(Table)}";
            }
        }

        public string Catalog { get; }
        public string Table { get; }
        public string Schema { get; }
        public string Instance { get; }
        public string Address { get; }

        public string QualifiedTableName
        {
            get
            {
                if (qualifiedTableName == null)
                {
                    throw new Exception("Qualified table name is only accessible for local addresses.");
                }
                return qualifiedTableName;
            }
        }

        string GetCanonicalForm()
        {
            return Instance != null 
                ? $"{Table}@{SafeQuote(Schema)}@{SafeQuote(Catalog)}@{Quote(Instance)}" 
                : $"{Table}@{Quote(Schema)}@{Quote(Catalog)}";
        }

        static string Quote(string name)
        {
            using (var sanitizer = new SqlCommandBuilder())
            {
                return sanitizer.QuoteIdentifier(name);
            }
        }

        static string SafeQuote(string name)
        {
            return name == null ? "[]" : Quote(name);
        }
    }
}