namespace NServiceBus.Transport.PostgreSql
{
    using System;

    class CanonicalQueueAddress
    {
        public CanonicalQueueAddress(string table, string schemaName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(table);
            ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);

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