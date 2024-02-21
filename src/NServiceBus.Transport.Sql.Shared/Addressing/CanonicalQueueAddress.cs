namespace NServiceBus.Transport.Sql.Shared.Addressing
{
    public class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName, string catalogName, INameHelper nameHelper)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(table);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
            ArgumentException.ThrowIfNullOrWhiteSpace(catalogName);

            Table = table;
            Catalog = catalogName;
            Schema = schemaName;
            Address = GetCanonicalForm(nameHelper);
            QuotedCatalogName = nameHelper.Quote(Catalog);
            QualifiedTableName = $"{nameHelper.Quote(Catalog)}.{nameHelper.Quote(Schema)}.{nameHelper.Quote(Table)}";
        }

        public string Catalog { get; }
        public string Table { get; }
        public string Schema { get; }
        public string Address { get; }

        public string QualifiedTableName { get; }
        public string QuotedCatalogName { get; }

        string GetCanonicalForm(INameHelper nameHelper)
        {
            return $"{Table}@{nameHelper.Quote(Schema)}@{nameHelper.Quote(Catalog)}";
        }
    }
}