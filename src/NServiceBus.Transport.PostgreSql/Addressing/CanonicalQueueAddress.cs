namespace NServiceBus.Transport.PostgreSql
{
    class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName, PostgreSqlNameHelper nameHelper)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);

            Table = table;
            Schema = schemaName;
            QualifiedTableName = $"{nameHelper.Quote(Schema)}.{nameHelper.Quote(Table)}";
            Address = QualifiedTableName;
        }

        public string Table { get; }
        public string Schema { get; }
        public string Address { get; }
        public string QualifiedTableName { get; }
    }
}