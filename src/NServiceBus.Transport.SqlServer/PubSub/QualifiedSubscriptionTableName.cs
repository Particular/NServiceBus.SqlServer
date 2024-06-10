namespace NServiceBus.Transport.SqlServer
{
    using System;

    class QualifiedSubscriptionTableName
    {
        public string QuotedCatalog;
        public string QuotedQualifiedName;

        public QualifiedSubscriptionTableName(string table, string schema, string catalog)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            QuotedCatalog = SqlServerNameHelper.Quote(catalog);
            QuotedQualifiedName = $"{SqlServerNameHelper.Quote(catalog)}.{SqlServerNameHelper.Quote(schema)}.{SqlServerNameHelper.Quote(table)}";
        }
    }
}