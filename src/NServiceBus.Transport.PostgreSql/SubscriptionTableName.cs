namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using Sql.Shared.Addressing;

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

        internal QualifiedSubscriptionTableName Qualify(string defaultSchema, string defaultCatalog, INameHelper nameHelper)
        {
            return new QualifiedSubscriptionTableName(table, schema ?? defaultSchema, catalog ?? defaultCatalog, nameHelper);
        }
    }
}