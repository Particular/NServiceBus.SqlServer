namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using Sql.Shared.Addressing;

    class QualifiedSubscriptionTableName
    {
        public string QuotedCatalog;
        public string QuotedQualifiedName;

        public QualifiedSubscriptionTableName(string table, string schema, string catalog, INameHelper nameHelper)
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

            QuotedCatalog = nameHelper.Quote(catalog);
            QuotedQualifiedName = $"{nameHelper.Quote(catalog)}.{nameHelper.Quote(schema)}.{nameHelper.Quote(table)}";
        }
    }
}