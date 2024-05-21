namespace NServiceBus.Transport.PostgreSql
{
    using System;

    class QualifiedSubscriptionTableName
    {
        public string QuotedCatalog;
        public string QuotedQualifiedName;

        public QualifiedSubscriptionTableName(string table, string schema)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            QuotedQualifiedName = $"{PostgreSqlNameHelper.Quote(schema)}.{PostgreSqlNameHelper.Quote(table)}";
        }
    }
}