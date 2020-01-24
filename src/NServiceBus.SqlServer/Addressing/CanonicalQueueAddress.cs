namespace NServiceBus.Transport.SqlServer
{
    using static NameHelper;

    class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName, string catalogName)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);
            Guard.AgainstNullAndEmpty(nameof(catalogName), catalogName);
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