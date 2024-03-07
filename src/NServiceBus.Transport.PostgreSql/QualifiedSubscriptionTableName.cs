namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using SqlServer;

    class QualifiedSubscriptionTableName
    {
        public string QuotedCatalog;
        public string QuotedQualifiedName;

        public QualifiedSubscriptionTableName(string table, string schema, PostgreSqlNameHelper nameHelper)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            QuotedQualifiedName = $"{nameHelper.Quote(schema)}.{nameHelper.Quote(table)}";
        }
    }
}