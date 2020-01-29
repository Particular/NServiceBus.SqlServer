namespace NServiceBus.Transport.SqlServer
{
    using System;

    class SubscriptionTableName
    {
        string table;
        string schema;
        string catalog;

        public SubscriptionTableName(string table, string schema, string catalog)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.schema = schema;
            this.catalog = catalog;
        }

        public QualifiedSubscriptionTableName Qualify(string defaultSchema, string defaultCatalog)
        {
            return new QualifiedSubscriptionTableName(table, schema ?? defaultSchema, catalog ?? defaultCatalog);
        }
    }
}