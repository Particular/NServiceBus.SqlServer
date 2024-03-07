namespace NServiceBus.Transport.PostgreSql
{
    using System;
    using SqlServer;

    /// <summary>
    /// Subscription table name.
    /// </summary>
    public class SubscriptionTableName
    {
        string table;
        string schema;

        /// <summary>
        /// Creates an instance of <see cref="SubscriptionTableName"/>
        /// </summary>
        /// <param name="table">Table name.</param>
        /// <param name="schema">Schema name.</param>
        public SubscriptionTableName(string table, string schema = null)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.schema = schema;
        }

        internal QualifiedSubscriptionTableName Qualify(string defaultSchema, PostgreSqlNameHelper nameHelper)
        {
            return new QualifiedSubscriptionTableName(table, schema ?? defaultSchema, nameHelper);
        }
    }
}