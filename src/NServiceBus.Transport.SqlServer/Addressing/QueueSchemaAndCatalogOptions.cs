namespace NServiceBus.Transport.SqlServer
{
    using System.Collections.Generic;

    /// <summary>
    /// Queue and schema settings for SQL Transport queues.
    /// </summary>
    public class QueueSchemaAndCatalogOptions
    {
        internal QueueSchemaAndCatalogOptions() { }

        /// <summary>
        /// Enables specifying schema for a given queue.
        /// </summary>
        public void UseSchemaForQueue(string queueName, string schema)
        {
            schemas[queueName] = schema;
        }

        /// <summary>
        /// Enables specifying catalog for a given queue.
        /// </summary>
        public void UseCatalogForQueue(string queueName, string catalog)
        {
            catalogs[queueName] = catalog;
        }

        internal void TryGet(string queueName, out string schema, out string catalog)
        {
            schemas.TryGetValue(queueName, out schema);
            catalogs.TryGetValue(queueName, out catalog);
        }

        Dictionary<string, string> schemas = new Dictionary<string, string>();
        Dictionary<string, string> catalogs = new Dictionary<string, string>();
    }
}