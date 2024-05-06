namespace NServiceBus.Transport.SqlServer
{
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
            QuotedCatalogName = SqlServerNameHelper.Quote(Catalog);
            QualifiedTableName = $"{SqlServerNameHelper.Quote(Catalog)}.{SqlServerNameHelper.Quote(Schema)}.{SqlServerNameHelper.Quote(Table)}";
        }

        public string Catalog { get; }
        public string Table { get; }
        public string Schema { get; }
        public string Address { get; }

        public string QualifiedTableName { get; }
        public string QuotedCatalogName { get; }

        string GetCanonicalForm()
        {
            return $"{Table}@{SqlServerNameHelper.Quote(Schema)}@{SqlServerNameHelper.Quote(Catalog)}";
        }
    }
}