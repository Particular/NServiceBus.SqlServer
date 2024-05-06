namespace NServiceBus.Transport.SqlServer
{
    using System;

    /// <summary>
    /// Subscription table name.
    /// </summary>
    public class SubscriptionTableName
    {
        string table;
        string schema;
        string catalog;

        /// <summary>
        /// Creates an instance of <see cref="SubscriptionTableName"/>
        /// </summary>
        /// <param name="table">Table name.</param>
        /// <param name="schema">Schema name.</param>
        /// <param name="catalog">Catalog name.</param>
        public SubscriptionTableName(string table, string schema = null, string catalog = null)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.schema = schema;
            this.catalog = catalog;
        }

        internal QualifiedSubscriptionTableName Qualify(string defaultSchema, string defaultCatalog)
        {
            return new QualifiedSubscriptionTableName(table, schema ?? defaultSchema, catalog ?? defaultCatalog);
        }
    }
}