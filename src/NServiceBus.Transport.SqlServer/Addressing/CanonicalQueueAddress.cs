namespace NServiceBus.Transport.SqlServer
{
    class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName, string catalogName, SqlServerNameHelper nameHelper)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);
            Guard.AgainstNullAndEmpty(nameof(catalogName), catalogName);
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

        string GetCanonicalForm(SqlServerNameHelper nameHelper)
        {
            return $"{Table}@{nameHelper.Quote(Schema)}@{nameHelper.Quote(Catalog)}";
        }
    }
}