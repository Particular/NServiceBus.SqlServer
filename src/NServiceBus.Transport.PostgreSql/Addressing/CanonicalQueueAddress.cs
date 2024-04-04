namespace NServiceBus.Transport.PostgreSql
{
    using SqlServer;

    class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName, PostgreSqlNameHelper nameHelper)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);

            Table = table;
            Schema = schemaName;
            Address = GetCanonicalForm(nameHelper);
            QualifiedTableName = $"{nameHelper.Quote(Schema)}.{nameHelper.Quote(Table)}";
        }

        public string Table { get; }
        public string Schema { get; }
        public string Address { get; }

        public string QualifiedTableName { get; }

        string GetCanonicalForm(PostgreSqlNameHelper nameHelper)
        {
            return $"{Table}@{nameHelper.Quote(Schema)}";
        }
    }
}