namespace NServiceBus.Transport.SQLServer
{
    using static NameHelper;

    class LegacyCanonicalQueueAddress
    {
        public LegacyCanonicalQueueAddress(string table, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Table = table;
            Schema = schemaName;
            Address = GetCanonicalForm();
            QualifiedTableName = $"{Quote(Schema)}.{Quote(Table)}";
        }

        public string Table { get; }
        public string Schema { get; }
        public string Address { get; }
        public string QualifiedTableName { get; }

        string GetCanonicalForm()
        {
            return $"{Table}@{Quote(Schema)}";
        }
    }
}