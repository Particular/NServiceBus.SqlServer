namespace NServiceBus.Transport.PostgreSql
{
    class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(table), table);
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);

            Table = table;
            Schema = schemaName;
            QualifiedTableName = $"{PostgreSqlNameHelper.Quote(Schema)}.{PostgreSqlNameHelper.Quote(Table)}";
            Address = QualifiedTableName;
        }

        public string Table { get; }
        public string Schema { get; }
        public string Address { get; }
        public string QualifiedTableName { get; }
    }
}