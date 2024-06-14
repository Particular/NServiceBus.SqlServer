namespace NServiceBus.Transport.SqlServer
{
    using System;
    using static NameHelper;

    class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName, string catalogName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(table);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
            ArgumentException.ThrowIfNullOrWhiteSpace(catalogName);

            Table = table;
            Catalog = catalogName;
            Schema = schemaName;
            Address = GetCanonicalForm();
            QualifiedTableName = $"{Quote(Catalog)}.{Quote(Schema)}.{Quote(Table)}";
        }

        public string Catalog { get; }
        public string Table { get; }
        public string Schema { get; }
        public string Address { get; }

        public string QualifiedTableName { get; }
        public string QuotedCatalogName => Quote(Catalog);

        string GetCanonicalForm()
        {
            return $"{Table}@{Quote(Schema)}@{Quote(Catalog)}";
        }
    }
}