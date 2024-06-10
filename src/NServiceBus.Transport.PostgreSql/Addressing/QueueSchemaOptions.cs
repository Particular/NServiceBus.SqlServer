namespace NServiceBus.Transport.PostgreSql
{
    using System.Collections.Generic;

    /// <summary>
    /// Queue and schema settings for SQL Transport queues.
    /// </summary>
    public class QueueSchemaOptions
    {
        internal QueueSchemaOptions() { }

        /// <summary>
        /// Enables specifying schema for a given queue.
        /// </summary>
        public void UseSchemaForQueue(string queueName, string schema)
        {
            schemas[queueName] = schema;
        }

        internal void TryGet(string queueName, out string schema)
        {
            schemas.TryGetValue(queueName, out schema);
        }

        Dictionary<string, string> schemas = [];
    }
}